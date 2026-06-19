using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private string? _token;

    public ApiClient(AppSettings settings)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public void SetToken(string? token) => _token = token;

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"api/{path.TrimStart('/')}");
        if (!string.IsNullOrEmpty(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    public async Task<LoginResponse> LoginAsync(string email, string senha, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { email, senha }, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(ParseApiError(error));
        }
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida do servidor");
        SetToken(result.AccessToken);
        return result;
    }

    public async Task RegisterUserAsync(string nome, string email, string senha, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", new { nome, email, senha }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DeviceDto>> GetMyDevicesAsync(CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "auth/devices");
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DeviceDto>>(_json, ct) ?? [];
    }

    public async Task<DeviceDto> RegisterDeviceAsync(
        string deviceUid, string nomePc, string senhaSessao, string tipo = "quick", CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/devices/register",
            new { device_uid = deviceUid, nome_pc = nomePc, senha_sessao = senhaSessao, tipo },
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceDto>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida");
    }

    public async Task<ConnectResponse> ConnectToDeviceAsync(
        string deviceUid, string senhaSessao, string sessionId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "devices/connect");
        request.Content = JsonContent.Create(new { device_uid = deviceUid, senha_sessao = senhaSessao, session_id = sessionId });

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Falha ao conectar: {error}");
        }

        return await response.Content.ReadFromJsonAsync<ConnectResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida");
    }

    public async Task<ConnectResponse> ConnectAsOperatorAsync(
        string deviceUid, string sessionId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "devices/connect-operator");
        request.Content = JsonContent.Create(new { device_uid = deviceUid, session_id = sessionId });

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(ParseApiError(error));
        }

        return await response.Content.ReadFromJsonAsync<ConnectResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida");
    }

    public async Task RecordDeviceAccessAsync(string deviceUid, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "devices/record-access");
        request.Content = JsonContent.Create(new { device_uid = deviceUid });
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(ParseApiError(error));
        }
    }

    private static string ParseApiError(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? raw;
        }
        catch { /* ignore */ }
        return raw;
    }
}
