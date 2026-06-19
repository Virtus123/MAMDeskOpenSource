using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public sealed class RemoteHostSession : IAsyncDisposable
{
    private readonly SignalingClient _deviceSignaling = new();
    private readonly SignalingClient _sessionSignaling = new();
    private readonly ScreenCaptureService _capture;
    private CancellationTokenSource? _streamCts;
    private CancellationTokenSource? _deviceHeartbeatCts;
    private CancellationTokenSource? _sessionHeartbeatCts;
    private string _wsUrl = "ws://localhost:8000";
    private string _deviceUid = string.Empty;
    private string? _activeSessionId;
    private bool _disposing;
    private bool _reconnectingDevice;
    private bool _reconnectingSession;
    private bool _endingSession;
    private string? _lastCursorStyle;
    private volatile int _sendInProgress;

    public event Action<string, bool>? ConnectionRequested;
    public event Action<string>? ChatReceived;
    public event Action? SessionEnded;
    public event Action? SessionActive;
    public event Action<string>? OperatorConnected;
    public event Action<DrawPayload>? DrawReceived;
    public event Action? DrawClearRequested;
    public event Action? RemoteEndSessionRequested;

    public RemoteHostSession(int videoQuality = 92, float scale = 0.95f)
    {
        _capture = new ScreenCaptureService(videoQuality, scale);
        _deviceSignaling.MessageReceived += OnDeviceMessage;
        _deviceSignaling.Disconnected += OnDeviceDisconnected;
        _sessionSignaling.MessageReceived += OnSessionMessage;
        _sessionSignaling.Disconnected += OnSessionDisconnected;
    }

    public void Configure(string wsUrl) => _wsUrl = wsUrl.TrimEnd('/');

    public async Task StartAsync(string deviceUid, CancellationToken ct = default)
    {
        _deviceUid = deviceUid;
        await ConnectDeviceWsAsync(ct);
        StartDeviceHeartbeat(ct);
    }

    private void StartDeviceHeartbeat(CancellationToken ct)
    {
        _deviceHeartbeatCts?.Cancel();
        _deviceHeartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => HeartbeatLoopAsync(_deviceSignaling, _deviceHeartbeatCts.Token), _deviceHeartbeatCts.Token);
    }

    private async Task ConnectDeviceWsAsync(CancellationToken ct)
    {
        await _deviceSignaling.ConnectDeviceAsync(_wsUrl, _deviceUid, ct);
    }

    private void OnDeviceDisconnected()
    {
        if (_disposing) return;
        _ = ReconnectDeviceAsync();
    }

    private async Task ReconnectDeviceAsync()
    {
        if (_disposing || _reconnectingDevice || string.IsNullOrEmpty(_deviceUid))
            return;

        _reconnectingDevice = true;
        try
        {
            for (var attempt = 0; attempt < 30 && !_disposing; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 + attempt, 8)));
                try
                {
                    await ConnectDeviceWsAsync(CancellationToken.None);
                    StartDeviceHeartbeat(CancellationToken.None);
                    return;
                }
                catch { /* retry */ }
            }
        }
        finally
        {
            _reconnectingDevice = false;
        }
    }

    public async Task RespondToConnectionAsync(string sessionId, bool accepted, CancellationToken ct = default)
    {
        if (!accepted)
        {
            await _deviceSignaling.SendAsync(new SignalingMessage
            {
                Type = "connection_response",
                SessionId = sessionId,
                Accepted = false,
            }, ct);
            return;
        }

        _activeSessionId = sessionId;
        _endingSession = false;

        await SendAcceptViaDeviceAsync(sessionId, ct);
        await ConnectSessionAsync(sessionId, resendAccept: false, ct);
    }

    private async Task SendAcceptViaDeviceAsync(string sessionId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                await _deviceSignaling.SendAsync(new SignalingMessage
                {
                    Type = "connection_response",
                    SessionId = sessionId,
                    Accepted = true,
                }, ct);
                return;
            }
            catch
            {
                await Task.Delay(250, ct);
            }
        }
    }

    private async Task ConnectSessionAsync(string sessionId, bool resendAccept, CancellationToken ct)
    {
        await _sessionSignaling.ConnectSessionAsync(_wsUrl, sessionId, "host", ct);

        if (resendAccept)
        {
            await _sessionSignaling.SendAsync(new SignalingMessage
            {
                Type = "connection_response",
                SessionId = sessionId,
                Accepted = true,
            }, ct);
        }

        _sessionHeartbeatCts?.Cancel();
        _sessionHeartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => HeartbeatLoopAsync(_sessionSignaling, _sessionHeartbeatCts.Token), _sessionHeartbeatCts.Token);

        StartStreaming();
        _ = SendMonitorsToViewerAsync(ct);
        _lastCursorStyle = null;
        NotifyCursorStyleIfChanged();
        SessionActive?.Invoke();
    }

    private async Task SendMonitorsToViewerAsync(CancellationToken ct)
    {
        try
        {
            await _sessionSignaling.SendAsync(new SignalingMessage
            {
                Type = "monitors",
                SessionId = _activeSessionId,
                Monitors = _capture.GetMonitors().ToList(),
            }, ct);
        }
        catch { /* ignore */ }
    }

    public async Task EndSessionFromHostAsync(CancellationToken ct = default)
    {
        try
        {
            await _sessionSignaling.SendAsync(new SignalingMessage
            {
                Type = "session_end",
                SessionId = _activeSessionId,
                Reason = "host_ended",
            }, ct);
        }
        catch { /* ignore */ }

        EndActiveSession(notify: true);
    }

    private void OnSessionDisconnected()
    {
        if (_disposing || _endingSession) return;
        _ = TryReconnectSessionAsync();
    }

    private async Task TryReconnectSessionAsync()
    {
        if (_disposing || _reconnectingSession || string.IsNullOrEmpty(_activeSessionId))
            return;

        _reconnectingSession = true;
        _streamCts?.Cancel();

        var sessionId = _activeSessionId;
        try
        {
            for (var attempt = 0; attempt < 8 && !_disposing && _activeSessionId == sessionId; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                try
                {
                    await ConnectSessionAsync(sessionId, resendAccept: true, CancellationToken.None);
                    return;
                }
                catch { /* retry */ }
            }

            EndActiveSession(notify: true);
        }
        finally
        {
            _reconnectingSession = false;
        }
    }

    private void EndActiveSession(bool notify)
    {
        if (_endingSession) return;
        _endingSession = true;

        _streamCts?.Cancel();
        _sessionHeartbeatCts?.Cancel();
        _activeSessionId = null;
        _ = _sessionSignaling.DisconnectAsync();

        if (notify)
            SessionEnded?.Invoke();
    }

    private void StartStreaming()
    {
        _streamCts?.Cancel();
        _streamCts = new CancellationTokenSource();
        var token = _streamCts.Token;

        _ = Task.Run(async () =>
        {
            var failures = 0;
            const int targetFrameMs = 16; // ~60 FPS alvo

            while (!token.IsCancellationRequested && _sessionSignaling.IsConnected)
            {
                var frameStart = Environment.TickCount64;
                try
                {
                    if (_sendInProgress > 0)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    var jpeg = await Task.Run(_capture.CaptureScreenJpeg, token);

                    _sendInProgress = 1;
                    _ = SendFrameAsync(jpeg, token).ContinueWith(
                        _ => Interlocked.Exchange(ref _sendInProgress, 0),
                        TaskScheduler.Default);

                    failures = 0;

                    var elapsed = (int)(Environment.TickCount64 - frameStart);
                    var delay = Math.Max(1, targetFrameMs - elapsed);
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    Interlocked.Exchange(ref _sendInProgress, 0);
                    failures++;
                    if (failures >= 5 || token.IsCancellationRequested)
                        break;
                    await Task.Delay(200, token);
                }
            }
        }, token);
    }

    private async Task SendFrameAsync(byte[] jpeg, CancellationToken token)
    {
        await _sessionSignaling.SendAsync(new SignalingMessage
        {
            Type = "frame",
            SessionId = _activeSessionId,
            FrameBase64 = Convert.ToBase64String(jpeg),
        }, token);
    }

    private async Task SendCursorStyleAsync(string style, CancellationToken ct = default)
    {
        try
        {
            await _sessionSignaling.SendAsync(new SignalingMessage
            {
                Type = "cursor",
                SessionId = _activeSessionId,
                Text = style,
            }, ct);
        }
        catch { /* ignore */ }
    }

    private void NotifyCursorStyleIfChanged()
    {
        var style = CursorHelper.GetCurrentStyleName();
        if (style == _lastCursorStyle) return;
        _lastCursorStyle = style;
        _ = SendCursorStyleAsync(style);
    }

    private void OnDeviceMessage(SignalingMessage msg)
    {
        if (msg.Type == "connection_request" && !string.IsNullOrEmpty(msg.SessionId))
            ConnectionRequested?.Invoke(msg.SessionId, msg.Trusted ?? false);
    }

    private void OnSessionMessage(SignalingMessage msg)
    {
        if (msg.Type == "session_closed")
        {
            if (!_endingSession && !_reconnectingSession)
                _ = TryReconnectSessionAsync();
            return;
        }

        if (msg.Type == "session_end")
        {
            RemoteEndSessionRequested?.Invoke();
            EndActiveSession(notify: true);
            return;
        }

        if (msg.Type == "draw" && msg.Draw is not null)
        {
            if (msg.Draw.Action == "clear")
                DrawClearRequested?.Invoke();
            else
                DrawReceived?.Invoke(msg.Draw);
            return;
        }

        if (msg.Type == "command" && msg.Command is not null)
            HandleCommand(msg.Command);

        if (msg.Type == "chat" && !string.IsNullOrEmpty(msg.Text))
            ChatReceived?.Invoke(msg.Text);

        if (msg.Type == "input" && msg.Input is not null)
        {
            InputSimulator.Apply(
                msg.Input,
                _capture.GetNativeScreenSize(),
                _capture.GetCaptureFrameSize());

            if (msg.Input.Tipo == "mouse")
                NotifyCursorStyleIfChanged();
        }
    }

    private void HandleCommand(SessionCommandPayload cmd)
    {
        switch (cmd.Command)
        {
            case "session_info":
                if (!string.IsNullOrWhiteSpace(cmd.OperatorName))
                    OperatorConnected?.Invoke(cmd.OperatorName);
                if (cmd.VideoQuality is int q && cmd.VideoScalePercent is int s)
                    _capture.UpdateSettings(q, s / 100f);
                break;
            case "set_monitor":
                if (cmd.MonitorIndex is int idx)
                {
                    _capture.SetMonitor(idx);
                    DrawClearRequested?.Invoke();
                    _ = SendMonitorsToViewerAsync(CancellationToken.None);
                }
                break;
        }
    }

    private static async Task HeartbeatLoopAsync(SignalingClient client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (client.IsConnected)
                    await client.SendPingAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
    }

    public ScreenCaptureService Capture => _capture;

    public async ValueTask DisposeAsync()
    {
        _disposing = true;
        _endingSession = true;
        _streamCts?.Cancel();
        _deviceHeartbeatCts?.Cancel();
        _sessionHeartbeatCts?.Cancel();
        await _deviceSignaling.DisposeAsync();
        await _sessionSignaling.DisposeAsync();
        _capture.Dispose();
    }
}
