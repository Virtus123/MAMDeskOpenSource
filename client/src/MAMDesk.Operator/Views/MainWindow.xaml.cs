using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MAMDesk.Shared.Models;
using MAMDesk.Shared.Services;

namespace MAMDesk.Operator.Views;

public partial class MainWindow : Window
{
    private readonly ApiClient _api;
    private RemoteViewerSession? _viewerSession;
    private bool _isConnecting;
    private bool _loadingDevices;

    public MainWindow(ApiClient api)
    {
        InitializeComponent();
        _api = api;
        var profile = UserProfileStore.Load();
        UserNameText.Text = !string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.DisplayName
            : App.Settings.UserName ?? "Operador";
        ServerUrlBox.Text = App.Settings.ServerUrl;
        LoadSettingsPanel(profile);
        ShowPanel(ConnectPanel);
    }

    private void NavConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        SetNavActive(NavConnectBtn);
        ShowPanel(ConnectPanel);
    }

    private async void NavDevicesBtn_Click(object sender, RoutedEventArgs e)
    {
        SetNavActive(NavDevicesBtn);
        ShowPanel(DevicesPanel);
        await LoadDevicesAsync();
    }

    private void NavSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        SetNavActive(NavSettingsBtn);
        ShowPanel(SettingsPanel);
        LoadSettingsPanel(UserProfileStore.Load());
    }

    private void LoadSettingsPanel(OperatorProfile profile)
    {
        DisplayNameBox.Text = profile.DisplayName;
        QualitySlider.Value = profile.VideoQuality;
        QualityValueText.Text = $"{profile.VideoQuality}%";
        ScaleSlider.Value = profile.VideoScalePercent;
        ScaleValueText.Text = $"{profile.VideoScalePercent}%";
        ServerUrlBox.Text = App.Settings.ServerUrl;
        ProfilePathText.Text = UserProfileStore.ProfilePath;
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValueText is not null)
            QualityValueText.Text = $"{(int)e.NewValue}%";
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValueText is not null)
            ScaleValueText.Text = $"{(int)e.NewValue}%";
    }

    private void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var profile = new OperatorProfile
        {
            DisplayName = DisplayNameBox.Text.Trim(),
            VideoQuality = (int)QualitySlider.Value,
            VideoScalePercent = (int)ScaleSlider.Value,
        };
        UserProfileStore.Save(profile);
        UserNameText.Text = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? App.Settings.UserName ?? "Operador"
            : profile.DisplayName;
        MessageBox.Show("Configurações salvas em %AppData%\\MAMDesk\\operator.json", "MAMDesk",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetNavActive(Button active)
    {
        foreach (var btn in new[] { NavConnectBtn, NavDevicesBtn, NavSettingsBtn })
            btn.Tag = btn == active ? "active" : null;
    }

    private void ShowPanel(Border panel)
    {
        ConnectPanel.Visibility = panel == ConnectPanel ? Visibility.Visible : Visibility.Collapsed;
        DevicesPanel.Visibility = panel == DevicesPanel ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = panel == SettingsPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RefreshDevicesBtn_Click(object sender, RoutedEventArgs e) => await LoadDevicesAsync();

    private async Task LoadDevicesAsync()
    {
        if (_loadingDevices) return;
        _loadingDevices = true;
        NavDevicesBtn.IsEnabled = false;
        RefreshDevicesBtn.IsEnabled = false;
        DevicesLoadingText.Visibility = Visibility.Visible;
        DevicesEmptyText.Visibility = Visibility.Collapsed;
        DevicesScrollViewer.Visibility = Visibility.Collapsed;

        try
        {
            var devices = await _api.GetMyDevicesAsync().ConfigureAwait(true);
            var items = devices.Select(d => new DeviceListItem(d)).ToList();
            DevicesList.ItemsSource = items;

            var empty = items.Count == 0;
            DevicesEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            DevicesScrollViewer.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            DevicesEmptyText.Visibility = Visibility.Visible;
            MessageBox.Show($"Erro ao carregar dispositivos: {ex.Message}", "MAMDesk",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            DevicesLoadingText.Visibility = Visibility.Collapsed;
            NavDevicesBtn.IsEnabled = true;
            RefreshDevicesBtn.IsEnabled = true;
            _loadingDevices = false;
        }
    }

    private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnecting) return;

        ConnectErrorText.Visibility = Visibility.Collapsed;
        ConnectBtn.Content = "Conectando...";
        ConnectBtn.IsEnabled = false;
        _isConnecting = true;

        try
        {
            var deviceUid = DeviceIdentity.NormalizeInputId(DeviceIdBox.Text);
            var password = PasswordBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(deviceUid) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Informe o ID e a senha do dispositivo.");

            await StartRemoteConnectionAsync(
                ct => _api.ConnectToDeviceAsync(deviceUid, password, ct.sessionId));
        }
        catch (Exception ex)
        {
            ConnectErrorText.Text = ex.Message;
            ConnectErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _isConnecting = false;
            ConnectBtn.Content = "Conectar";
            ConnectBtn.IsEnabled = true;
        }
    }

    private async void AccessDeviceBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnecting) return;
        if (sender is not Button { Tag: DeviceListItem item }) return;

        if (!item.Online)
        {
            MessageBox.Show("Este dispositivo está offline.", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isConnecting = true;
        try
        {
            await StartRemoteConnectionAsync(
                ct => _api.ConnectAsOperatorAsync(item.RawUid, ct.sessionId));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível conectar: {ex.Message}", "MAMDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private async Task StartRemoteConnectionAsync(
        Func<(string sessionId, CancellationToken ct), Task<ConnectResponse>> requestConnection)
    {
        var sessionId = Guid.NewGuid().ToString();
        var acceptedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await (_viewerSession?.DisposeAsync() ?? ValueTask.CompletedTask);
        _viewerSession = new RemoteViewerSession();
        _viewerSession.ConnectionAccepted += OnAccepted;

        try
        {
            // 1. Operador entra na sessão WebSocket ANTES de avisar o dispositivo
            await _viewerSession.ConnectAsync(App.Settings.WsUrl, sessionId);

            // 2. Servidor envia pedido ao Quick Support
            var response = await requestConnection((sessionId, CancellationToken.None));

            // 3. Aguarda aceite (auto ou manual)
            var completed = await Task.WhenAny(acceptedTcs.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            if (completed != acceptedTcs.Task)
                throw new TimeoutException("Tempo esgotado. Verifique se o Quick Support está aberto.");

            if (!await acceptedTcs.Task)
                throw new InvalidOperationException("O cliente recusou a conexão.");

            _viewerSession.ConnectionAccepted -= OnAccepted;
            _viewerSession.MarkHandshakeComplete();

            var profile = UserProfileStore.Load();
            var operatorName = !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? profile.DisplayName
                : App.Settings.UserName ?? "Operador";

            var remoteWindow = new RemoteViewWindow(_viewerSession, response.NomePc, operatorName, profile);
            remoteWindow.Closed += (_, _) => { _viewerSession = null; };
            remoteWindow.Show();

            try
            {
                await _api.RecordDeviceAccessAsync(response.DeviceUid);
            }
            catch { /* histórico é best-effort */ }

            await LoadDevicesAsync();
        }
        catch
        {
            if (_viewerSession is not null)
            {
                _viewerSession.ConnectionAccepted -= OnAccepted;
                await _viewerSession.DisposeAsync();
            }
            _viewerSession = null;
            throw;
        }

        void OnAccepted(bool ok) => acceptedTcs.TrySetResult(ok);
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_viewerSession is not null)
            await _viewerSession.DisposeAsync();
        base.OnClosed(e);
    }

    private sealed class DeviceListItem
    {
        public DeviceListItem(DeviceDto d)
        {
            NomePc = string.IsNullOrWhiteSpace(d.NomePc) ? "Computador" : d.NomePc;
            RawUid = d.DeviceUid ?? string.Empty;
            DeviceUid = string.IsNullOrWhiteSpace(d.DeviceUid)
                ? "—"
                : DeviceIdentity.FormatDisplayId(d.DeviceUid);
            Online = d.Online;
            StatusLabel = d.Online ? "Online" : "Offline";
            TipoLabel = d.Tipo == "quick" ? "Quick Support" : (d.Tipo ?? "—");
            DetailLine = $"{StatusLabel} · {TipoLabel}";
            OnlineIndicatorBrush = d.Online
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        public string NomePc { get; }
        public string RawUid { get; }
        public string DeviceUid { get; }
        public bool Online { get; }
        public string StatusLabel { get; }
        public string TipoLabel { get; }
        public string DetailLine { get; }
        public SolidColorBrush OnlineIndicatorBrush { get; }
    }
}
