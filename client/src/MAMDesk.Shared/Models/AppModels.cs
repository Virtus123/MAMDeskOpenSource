using System.Text.Json.Serialization;
using MAMDesk.Shared.Services;

namespace MAMDesk.Shared.Models;

public sealed class AppSettings
{
    public string ServerUrl { get; set; } = SettingsLoader.DefaultServerUrl;
    public string WsUrl { get; set; } = SettingsLoader.DefaultWsUrl;
}

public sealed class UserDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "bearer";

    [JsonPropertyName("usuario")]
    public UserDto Usuario { get; set; } = new();
}

public sealed class DeviceDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("device_uid")]
    public string DeviceUid { get; set; } = string.Empty;

    [JsonPropertyName("nome_pc")]
    public string NomePc { get; set; } = string.Empty;

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("ultimo_ip")]
    public string? UltimoIp { get; set; }

    [JsonPropertyName("ultima_conexao")]
    public DateTime? UltimaConexao { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "quick";
}

public sealed class ConnectResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("device_uid")]
    public string DeviceUid { get; set; } = string.Empty;

    [JsonPropertyName("nome_pc")]
    public string NomePc { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class SignalingMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("accepted")]
    public bool? Accepted { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("sdp")]
    public string? Sdp { get; set; }

    [JsonPropertyName("candidate")]
    public string? Candidate { get; set; }

    [JsonPropertyName("sdp_mid")]
    public string? SdpMid { get; set; }

    [JsonPropertyName("sdp_m_line_index")]
    public int? SdpMLineIndex { get; set; }

    [JsonPropertyName("input")]
    public InputPayload? Input { get; set; }

    [JsonPropertyName("frame_base64")]
    public string? FrameBase64 { get; set; }

    [JsonPropertyName("trusted")]
    public bool? Trusted { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("draw")]
    public DrawPayload? Draw { get; set; }

    [JsonPropertyName("command")]
    public SessionCommandPayload? Command { get; set; }

    [JsonPropertyName("monitors")]
    public List<MonitorInfoDto>? Monitors { get; set; }
}

public sealed class InputPayload
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("click")]
    public string? Click { get; set; }

    [JsonPropertyName("delta")]
    public int? Delta { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("vk")]
    public int? Vk { get; set; }

    [JsonPropertyName("down")]
    public bool? Down { get; set; }
}

public sealed class OperatorProfile
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("video_quality")]
    public int VideoQuality { get; set; } = 92;

    [JsonPropertyName("video_scale_percent")]
    public int VideoScalePercent { get; set; } = 95;
}

public sealed class MonitorInfoDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }
}

public sealed class DrawPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#EF4444";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 3;

    [JsonPropertyName("x1")]
    public int? X1 { get; set; }

    [JsonPropertyName("y1")]
    public int? Y1 { get; set; }

    [JsonPropertyName("x2")]
    public int? X2 { get; set; }

    [JsonPropertyName("y2")]
    public int? Y2 { get; set; }

    [JsonPropertyName("points")]
    public List<DrawPointDto>? Points { get; set; }
}

public sealed class DrawPointDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public sealed class SessionCommandPayload
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("monitor_index")]
    public int? MonitorIndex { get; set; }

    [JsonPropertyName("video_quality")]
    public int? VideoQuality { get; set; }

    [JsonPropertyName("video_scale_percent")]
    public int? VideoScalePercent { get; set; }
}
