using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public sealed class SignalingClient : IAsyncDisposable
{
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private bool _intentionalClose;

    public event Action<SignalingMessage>? MessageReceived;
    public event Action? Disconnected;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectDeviceAsync(string wsBaseUrl, string deviceUid, CancellationToken ct = default)
    {
        await ConnectInternalAsync($"{wsBaseUrl.TrimEnd('/')}/ws/device/{deviceUid}", ct);
    }

    public async Task ConnectSessionAsync(
        string wsBaseUrl, string sessionId, string role, CancellationToken ct = default)
    {
        await ConnectInternalAsync($"{wsBaseUrl.TrimEnd('/')}/ws/session/{sessionId}/{role}", ct);
    }

    private async Task ConnectInternalAsync(string url, CancellationToken ct)
    {
        await DisconnectAsync();
        _intentionalClose = false;
        _ws = new ClientWebSocket
        {
            Options = { KeepAliveInterval = TimeSpan.FromSeconds(15) },
        };
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await _ws.ConnectAsync(new Uri(url), _cts.Token);
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    public async Task SendAsync(SignalingMessage message, CancellationToken ct = default)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket não conectado");

        await _sendLock.WaitAsync(ct);
        try
        {
            if (_ws is null || _ws.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket não conectado");

            var json = JsonSerializer.Serialize(message, _json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task SendPingAsync(CancellationToken ct = default)
    {
        await SendAsync(new SignalingMessage { Type = "ping" }, ct);
    }

    private async Task ReceiveLoopAsync()
    {
        if (_ws is null || _cts is null) return;

        var buffer = new byte[8192];

        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None);
                        }
                        catch { /* ignore */ }
                        RaiseDisconnected();
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                var message = JsonSerializer.Deserialize<SignalingMessage>(json, _json);
                if (message is not null && message.Type != "pong")
                    MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            RaiseDisconnected();
        }
    }

    private void RaiseDisconnected()
    {
        if (!_intentionalClose)
            Disconnected?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        _intentionalClose = true;

        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_ws is not null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
                catch { /* ignore */ }
            }
            _ws.Dispose();
            _ws = null;
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask; } catch { /* ignore */ }
            _receiveTask = null;
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
