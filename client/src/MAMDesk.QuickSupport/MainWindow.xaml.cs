using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MAMDesk.QuickSupport.Views;
using MAMDesk.Shared.Models;
using MAMDesk.Shared.Services;

namespace MAMDesk.QuickSupport;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = SettingsLoader.Load();
    private readonly DeviceIdentity _identity = new();
    private readonly ApiClient _api;
    private readonly RemoteHostSession _hostSession = new();
    private HostSessionUiController? _sessionUi;
    private string _deviceUid = string.Empty;
    private string _password = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        _api = new ApiClient(_settings);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _deviceUid = _identity.GetOrCreateDeviceUid();
        _password = DeviceIdentity.GeneratePassword();

        DeviceIdText.Text = DeviceIdentity.FormatDisplayId(_deviceUid);
        PasswordText.Text = _password;

        _hostSession.Configure(_settings.WsUrl);
        _hostSession.ConnectionRequested += OnConnectionRequested;
        _hostSession.SessionEnded += OnSessionEnded;
        _sessionUi = new HostSessionUiController(_hostSession);

        try
        {
            await _api.RegisterDeviceAsync(
                _deviceUid,
                Environment.MachineName,
                _password,
                "quick");

            await _hostSession.StartAsync(_deviceUid);
            SetStatus("Aguardando conexão...", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Erro: {ex.Message}", false);
        }
    }

    private void OnSessionEnded()
    {
        Dispatcher.Invoke(() => SetStatus("Aguardando conexão...", true));
    }

    private async void OnConnectionRequested(string sessionId, bool trusted)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        if (trusted)
        {
            try
            {
                await _hostSession.RespondToConnectionAsync(sessionId, accepted: true);
                Dispatcher.Invoke(() => SetStatus("Conexão ativa (operador)", true));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Erro na conexão: {ex.Message}", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetStatus("Aguardando conexão...", true);
                });
            }
            return;
        }

        await Dispatcher.InvokeAsync(async () =>
        {
            Activate();
            Topmost = true;
            Topmost = false;

            var result = MessageBox.Show(
                "Um operador deseja acessar seu computador.\n\nDeseja permitir?",
                "Pedido de conexão - MAMDesk",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            var accepted = result == MessageBoxResult.Yes;
            SetStatus(accepted ? "Conexão ativa" : "Conexão recusada", accepted);

            try
            {
                await _hostSession.RespondToConnectionAsync(sessionId, accepted);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro na conexão: {ex.Message}", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Aguardando conexão...", true);
            }
        });
    }

    private void CopyIdBtn_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(DeviceIdentity.FormatDisplayId(_deviceUid));
        ShowCopyFeedback("ID copiado!");
    }

    private void CopyPasswordBtn_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_password);
        ShowCopyFeedback("Senha copiada!");
    }

    private void CopyBothBtn_Click(object sender, RoutedEventArgs e)
    {
        var id = DeviceIdentity.FormatDisplayId(_deviceUid);
        Clipboard.SetText($"ID: {id}{Environment.NewLine}Senha: {_password}");
        ShowCopyFeedback("ID e senha copiados!");
    }

    private void ShowCopyFeedback(string message)
    {
        CopyFeedbackText.Text = message;
        CopyFeedbackText.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            CopyFeedbackText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private async void RefreshPasswordBtn_Click(object sender, RoutedEventArgs e)
    {
        _password = DeviceIdentity.GeneratePassword();
        PasswordText.Text = _password;

        try
        {
            await _api.RegisterDeviceAsync(_deviceUid, Environment.MachineName, _password, "quick");
            MessageBox.Show("Nova senha gerada!", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao atualizar senha: {ex.Message}", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetStatus(string text, bool online)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(online ? Color.FromRgb(34, 197, 94) : Color.FromRgb(239, 68, 68));
    }

    protected override async void OnClosed(EventArgs e)
    {
        _sessionUi?.Dispose();
        await _hostSession.DisposeAsync();
        base.OnClosed(e);
    }
}
