using MAMDesk.Shared.Services;

namespace MAMDesk.QuickSupport.Views;

public sealed class HostSessionUiController : IDisposable
{
    private readonly RemoteHostSession _session;
    private ActiveSessionWindow? _panel;
    private HostDrawOverlayWindow? _overlay;

    public HostSessionUiController(RemoteHostSession session)
    {
        _session = session;
        _session.OperatorConnected += OnOperatorConnected;
        _session.SessionActive += OnSessionActive;
        _session.SessionEnded += OnSessionEnded;
        _session.DrawReceived += OnDrawReceived;
        _session.DrawClearRequested += OnDrawClear;
        _session.RemoteEndSessionRequested += OnSessionEnded;
    }

    private void OnOperatorConnected(string name)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            EnsurePanel();
            _panel!.SetOperatorName(name);
            _panel.Show();
        });
    }

    private void OnSessionActive()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _overlay?.Close();
            _overlay = new HostDrawOverlayWindow(_session.Capture);
            _overlay.Show();
            EnsurePanel();
            if (_panel!.Visibility != System.Windows.Visibility.Visible)
                _panel.Show();
        });
    }

    private void OnDrawReceived(MAMDesk.Shared.Models.DrawPayload draw) =>
        System.Windows.Application.Current.Dispatcher.Invoke(() => _overlay?.ApplyDraw(draw));

    private void OnDrawClear() =>
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _overlay?.Clear();
            _overlay?.SyncBounds();
        });

    private void OnSessionEnded()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _panel?.Hide();
            _overlay?.Close();
            _overlay = null;
        });
    }

    private void EnsurePanel()
    {
        if (_panel is not null) return;
        _panel = new ActiveSessionWindow();
        _panel.EndSessionRequested += () => _ = _session.EndSessionFromHostAsync();
    }

    public void Dispose()
    {
        _session.OperatorConnected -= OnOperatorConnected;
        _session.SessionActive -= OnSessionActive;
        _session.SessionEnded -= OnSessionEnded;
        _session.DrawReceived -= OnDrawReceived;
        _session.DrawClearRequested -= OnDrawClear;
        _session.RemoteEndSessionRequested -= OnSessionEnded;
        _panel?.Close();
        _overlay?.Close();
    }
}
