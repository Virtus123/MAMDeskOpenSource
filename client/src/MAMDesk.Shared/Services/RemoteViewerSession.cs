using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

/// <summary>
/// Sessão remota no lado do operador (viewer).
/// Recebe frames, envia mouse/teclado e chat.
/// </summary>
public sealed class RemoteViewerSession : IAsyncDisposable
{
    private readonly SignalingClient _signaling = new();
    private CancellationTokenSource? _heartbeatCts;
    private string? _sessionId;
    private string _wsUrl = string.Empty;
    private bool _disposed;
    private bool _reconnecting;
    private bool _disconnectNotified;
    private bool _handshakeComplete;

    public event Action<byte[]>? FrameReceived;
    public event Action<string>? ChatReceived;
    public event Action<bool>? ConnectionAccepted;
    public event Action<string>? SessionDisconnected;
    public event Action<IReadOnlyList<MonitorInfoDto>>? MonitorsReceived;
    public event Action? SessionEndedRemotely;
    public event Action<string>? CursorStyleReceived;

    private int _pendingMoveX;
    private int _pendingMoveY;
    private int _moveSendScheduled;

    public bool IsConnected => _signaling.IsConnected;

    public void MarkHandshakeComplete() => _handshakeComplete = true;

    public async Task ConnectAsync(string wsUrl, string sessionId, CancellationToken ct = default)
    {
        _wsUrl = wsUrl.TrimEnd('/');
        _sessionId = sessionId;
        _handshakeComplete = false;
        _disconnectNotified = false;
        _signaling.MessageReceived += OnMessage;
        _signaling.Disconnected += OnSignalingDisconnected;
        await _signaling.ConnectSessionAsync(_wsUrl, sessionId, "viewer", ct);
        StartHeartbeat(ct);
    }

    private void StartHeartbeat(CancellationToken ct)
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token), _heartbeatCts.Token);
    }

    public async Task SendMouseAsync(int x, int y, string? click = null, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "input",
            SessionId = _sessionId,
            Input = new InputPayload { Tipo = "mouse", X = x, Y = y, Click = click },
        }, ct);
    }

    public async Task SendMouseWheelAsync(int x, int y, int delta, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "input",
            SessionId = _sessionId,
            Input = new InputPayload { Tipo = "mouse", X = x, Y = y, Click = "wheel", Delta = delta },
        }, ct);
    }

    /// <summary>Envia movimento do mouse com coalescing (~250 Hz máx) para baixa latência.</summary>
    public void SendMouseMove(int x, int y)
    {
        _pendingMoveX = x;
        _pendingMoveY = y;
        if (Interlocked.CompareExchange(ref _moveSendScheduled, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3);
                var px = _pendingMoveX;
                var py = _pendingMoveY;
                await _signaling.SendAsync(new SignalingMessage
                {
                    Type = "input",
                    SessionId = _sessionId,
                    Input = new InputPayload { Tipo = "mouse", X = px, Y = py, Click = "move" },
                });
            }
            catch { /* ignore */ }
            finally
            {
                Interlocked.Exchange(ref _moveSendScheduled, 0);
            }
        });
    }

    public async Task SendKeyVkAsync(int vk, bool down, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "input",
            SessionId = _sessionId,
            Input = new InputPayload { Tipo = "keyboard", Vk = vk, Down = down },
        }, ct);
    }

    public async Task SendKeyAsync(string key, bool down, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "input",
            SessionId = _sessionId,
            Input = new InputPayload { Tipo = "keyboard", Key = key, Down = down },
        }, ct);
    }

    public async Task SendChatAsync(string text, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "chat",
            SessionId = _sessionId,
            Text = text,
        }, ct);
    }

    public async Task SendDrawAsync(DrawPayload draw, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "draw",
            SessionId = _sessionId,
            Draw = draw,
        }, ct);
    }

    public async Task SendSessionInfoAsync(string operatorName, int videoQuality, int videoScalePercent, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "command",
            SessionId = _sessionId,
            Command = new SessionCommandPayload
            {
                Command = "session_info",
                OperatorName = operatorName,
                VideoQuality = videoQuality,
                VideoScalePercent = videoScalePercent,
            },
        }, ct);
    }

    public async Task SendSetMonitorAsync(int monitorIndex, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "command",
            SessionId = _sessionId,
            Command = new SessionCommandPayload
            {
                Command = "set_monitor",
                MonitorIndex = monitorIndex,
            },
        }, ct);
    }

    public async Task SendSpecialAsync(string key, CancellationToken ct = default)
    {
        await _signaling.SendAsync(new SignalingMessage
        {
            Type = "input",
            SessionId = _sessionId,
            Input = new InputPayload { Tipo = "special", Key = key },
        }, ct);
    }

    public async Task SendEndSessionAsync(CancellationToken ct = default)
    {
        try
        {
            await _signaling.SendAsync(new SignalingMessage
            {
                Type = "session_end",
                SessionId = _sessionId,
                Reason = "viewer_ended",
            }, ct);
        }
        catch { /* ignore */ }
    }

    private void OnMessage(SignalingMessage msg)
    {
        switch (msg.Type)
        {
            case "connection_response":
                ConnectionAccepted?.Invoke(msg.Accepted ?? false);
                break;
            case "frame":
                if (!string.IsNullOrEmpty(msg.FrameBase64))
                    FrameReceived?.Invoke(Convert.FromBase64String(msg.FrameBase64));
                break;
            case "chat":
                if (!string.IsNullOrEmpty(msg.Text))
                    ChatReceived?.Invoke(msg.Text);
                break;
            case "session_closed":
                if (_handshakeComplete)
                    _ = TryReconnectAsync(msg.Reason ?? "Conexão encerrada pelo host");
                break;
            case "session_end":
                SessionEndedRemotely?.Invoke();
                NotifyDisconnected(msg.Reason ?? "Conexão encerrada");
                break;
            case "monitors":
                if (msg.Monitors is { Count: > 0 })
                    MonitorsReceived?.Invoke(msg.Monitors);
                break;
            case "cursor":
                if (!string.IsNullOrEmpty(msg.Text))
                    CursorStyleReceived?.Invoke(msg.Text);
                break;
        }
    }

    private void OnSignalingDisconnected()
    {
        if (!_disposed && _handshakeComplete)
            _ = TryReconnectAsync("Conexão perdida com o servidor");
    }

    private async Task TryReconnectAsync(string failReason)
    {
        if (!_handshakeComplete || _disposed || _reconnecting || string.IsNullOrEmpty(_sessionId))
            return;

        _reconnecting = true;
        try
        {
            for (var attempt = 0; attempt < 8 && !_disposed; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                try
                {
                    await _signaling.ConnectSessionAsync(_wsUrl, _sessionId!, "viewer");
                    _disconnectNotified = false;
                    StartHeartbeat(CancellationToken.None);
                    return;
                }
                catch { /* retry */ }
            }

            NotifyDisconnected(failReason);
        }
        finally
        {
            _reconnecting = false;
        }
    }

    private void NotifyDisconnected(string reason)
    {
        if (_disposed || _disconnectNotified) return;
        _disconnectNotified = true;
        SessionDisconnected?.Invoke(reason);
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_signaling.IsConnected)
                    await _signaling.SendPingAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                if (!_disposed && !_reconnecting && _handshakeComplete)
                    _ = TryReconnectAsync("Conexão perdida com o servidor");
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _heartbeatCts?.Cancel();
        await _signaling.DisposeAsync();
    }
}
