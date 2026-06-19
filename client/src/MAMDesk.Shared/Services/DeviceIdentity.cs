using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MAMDesk.Shared.Services;

public sealed class DeviceIdentity
{
    private readonly string _configPath;

    public DeviceIdentity()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MAMDesk");
        Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "device.json");
    }

    public string GetOrCreateDeviceUid()
    {
        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            var data = JsonSerializer.Deserialize<DeviceConfig>(json);
            if (!string.IsNullOrWhiteSpace(data?.DeviceUid))
                return data.DeviceUid;
        }

        var uid = GenerateUid();
        Save(uid);
        return uid;
    }

    public static string FormatDisplayId(string deviceUid)
    {
        var clean = deviceUid.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (clean.Length <= 9)
        {
            return string.Join(" ", Enumerable.Range(0, (clean.Length + 2) / 3)
                .Select(i => clean.Substring(i * 3, Math.Min(3, clean.Length - i * 3))));
        }
        // IDs antigos (32 chars hex): exibe os primeiros 9
        clean = clean[..9];
        return string.Join(" ", Enumerable.Range(0, 3).Select(i => clean.Substring(i * 3, 3)));
    }

    public static string NormalizeInputId(string input)
    {
        return input.Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
    }

    public static string GeneratePassword(int length = 6)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private void Save(string deviceUid)
    {
        var config = new DeviceConfig { DeviceUid = deviceUid };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(config));
    }

    private static string GenerateUid()
    {
        // ID curto de 9 dígitos — igual ao exibido na tela (estilo AnyDesk)
        return RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString();
    }

    private sealed class DeviceConfig
    {
        public string DeviceUid { get; set; } = string.Empty;
    }
}
