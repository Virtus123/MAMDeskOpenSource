using System.IO;
using System.Windows;

namespace MAMDesk.QuickSupport;

public partial class App : Application
{
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

    private static void ShowFatal(Exception ex)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MAMDesk", "quicksupport-crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"[{DateTime.Now:O}] {ex}\n");
        }
        catch { /* ignore */ }

        MessageBox.Show(
            $"O Quick Support encontrou um erro:\n\n{ex.Message}\n\nDetalhes em %LocalAppData%\\MAMDesk\\",
            "MAMDesk Quick Support", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
