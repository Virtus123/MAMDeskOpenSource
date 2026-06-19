using System.IO;
using System.Windows;

namespace MAMDesk.Operator;

public partial class App : Application
{
    public static AppSettingsHolder Settings { get; } = LoadSettingsSafe();

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            ShowFatal(e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ShowFatal(ex);
        };
    }

    private static AppSettingsHolder LoadSettingsSafe()
    {
        try
        {
            return AppSettingsHolder.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao iniciar configuracoes: {ex.Message}", "MAMDesk",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return new AppSettingsHolder();
        }
    }

    private static void ShowFatal(Exception ex)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MAMDesk", "operator-crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"[{DateTime.Now:O}] {ex}\n");
        }
        catch { /* ignore */ }

        MessageBox.Show(
            $"O MAMDesk encontrou um erro:\n\n{ex.Message}\n\nDetalhes salvos em %LocalAppData%\\MAMDesk\\",
            "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public sealed class AppSettingsHolder
{
    public string ServerUrl { get; init; } = "http://localhost:8100";
    public string WsUrl { get; init; } = "ws://localhost:8100";
    public string? Token { get; set; }
    public string? UserName { get; set; }

    public static AppSettingsHolder Load()
    {
        var s = MAMDesk.Shared.Services.SettingsLoader.Load();
        var profile = MAMDesk.Shared.Services.UserProfileStore.Load();
        return new AppSettingsHolder
        {
            ServerUrl = s.ServerUrl,
            WsUrl = s.WsUrl,
            UserName = !string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.DisplayName : null,
        };
    }
}
