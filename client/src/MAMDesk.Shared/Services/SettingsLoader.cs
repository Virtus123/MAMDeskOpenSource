using System.IO;
using System.Text.Json;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public static class SettingsLoader
{
    public const string DefaultServerUrl = "http://191.252.208.95:8100";
    public const string DefaultWsUrl = "ws://191.252.208.95:8100";

    public static AppSettings Load(string? appBasePath = null)
    {
        var settings = new AppSettings
        {
            ServerUrl = Environment.GetEnvironmentVariable("MAMDESK_SERVER") ?? DefaultServerUrl,
            WsUrl = Environment.GetEnvironmentVariable("MAMDESK_WS") ?? DefaultWsUrl,
        };

        try
        {
            var basePath = appBasePath ?? AppContext.BaseDirectory;
            var path = Path.Combine(basePath, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var file = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (file is not null)
                {
                    if (!string.IsNullOrWhiteSpace(file.ServerUrl)) settings.ServerUrl = file.ServerUrl;
                    if (!string.IsNullOrWhiteSpace(file.WsUrl)) settings.WsUrl = file.WsUrl;
                }
            }
        }
        catch { /* usa defaults */ }

        return settings;
    }
}
