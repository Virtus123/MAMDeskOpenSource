using System.IO;
using System.Text.Json;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public static class UserProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static string ProfileDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAMDesk");

    public static string ProfilePath => Path.Combine(ProfileDirectory, "operator.json");

    public static OperatorProfile Load()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                var json = File.ReadAllText(ProfilePath);
                return JsonSerializer.Deserialize<OperatorProfile>(json, Json) ?? new OperatorProfile();
            }
        }
        catch { /* ignore */ }

        return new OperatorProfile();
    }

    public static void Save(OperatorProfile profile)
    {
        Directory.CreateDirectory(ProfileDirectory);
        var json = JsonSerializer.Serialize(profile, Json);
        File.WriteAllText(ProfilePath, json);
    }
}
