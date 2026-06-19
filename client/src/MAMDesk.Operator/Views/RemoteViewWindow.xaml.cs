using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MAMDesk.Shared.Models;
using MAMDesk.Shared.Services;

namespace MAMDesk.Operator.Views;

public partial class RemoteViewWindow : Window
{
    private enum DrawTool { Pointer, Pen, Rect, Ellipse, Arrow }

    private readonly RemoteViewerSession _session;
    private readonly OperatorProfile _profile;
    private readonly string _operatorName;
    private readonly LowLevelKeyboardHook _keyboardHook = new();
    private readonly DispatcherTimer _stallTimer;
    private readonly List<DrawPointDto> _strokePoints = new();

    private DrawTool _tool = DrawTool.Pointer;
    private string _drawColor = "#EF4444";
    private int _strokeWidth = 4;
    private int _frameWidth = 1;
    private int _frameHeight = 1;
    private bool _isDragging;
    private bool _isDrawing;
    private bool _sessionClosed;
    private int _shapeStartX;
    private int _shapeStartY;
    private Shape? _previewShape;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private IntPtr _windowHandle;
    private bool _updatingMonitors;
    private bool _toolbarExpanded = true;

    public RemoteViewWindow(RemoteViewerSession session, string remoteName, string operatorName, OperatorProfile profile)
    {
        InitializeComponent();
        _session = session;
        _operatorName = operatorName;
        _profile = profile;
        Title = $"MAMDesk - {remoteName}";
        var status = $"{operatorName} → {remoteName}";
        StatusText.Text = status;
        StatusTextCollapsed.Text = status;

        _session.FrameReceived += OnFrameReceived;
        _session.SessionDisconnected += OnSessionDisconnected;
        _session.MonitorsReceived += OnMonitorsReceived;
        _session.CursorStyleReceived += OnCursorStyleReceived;
        _session.SessionEndedRemotely += () => Dispatcher.Invoke(Close);

        VideoArea.Cursor = Cursors.Arrow;

        _stallTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _stallTimer.Tick += StallTimer_Tick;

        Loaded += OnLoaded;
        Closed += OnClosedCleanup;
        MouseDown += (_, _) => ActivateAndFocus();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        ActivateAndFocus();
        _keyboardHook.Install(ShouldInterceptKey, OnHookKey);
        _stallTimer.Start();

        try
        {
            await _session.SendSessionInfoAsync(
                _operatorName,
                _profile.VideoQuality,
                _profile.VideoScalePercent);
        }
        catch { /* ignore */ }
    }

    private void OnCursorStyleReceived(string style)
    {
        Dispatcher.Invoke(() =>
        {
            VideoArea.Cursor = style switch
            {
                "ibeam" => Cursors.IBeam,
                "hand" => Cursors.Hand,
                "wait" => Cursors.Wait,
                "cross" => Cursors.Cross,
                "sizeall" => Cursors.SizeAll,
                "sizens" => Cursors.SizeNS,
                "sizewe" => Cursors.SizeWE,
                "no" => Cursors.No,
                _ => Cursors.Arrow,
            };
        });
    }

    private void OnMonitorsReceived(IReadOnlyList<MonitorInfoDto> monitors)
    {
        Dispatcher.Invoke(() =>
        {
            _updatingMonitors = true;
            MonitorCombo.ItemsSource = monitors;
            if (monitors.Count > 0)
                MonitorCombo.SelectedIndex = 0;
            _updatingMonitors = false;
        });
    }

    private void ToolBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;
        foreach (var t in new[] { ToolPointerBtn, ToolPenBtn, ToolRectBtn, ToolEllipseBtn, ToolArrowBtn })
            t.IsChecked = t == btn;

        _tool = tag switch
        {
            "pen" => DrawTool.Pen,
            "rect" => DrawTool.Rect,
            "ellipse" => DrawTool.Ellipse,
            "arrow" => DrawTool.Arrow,
            _ => DrawTool.Pointer,
        };
    }

    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
            _drawColor = color;
    }

    private void StrokeWidthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StrokeWidthCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var w))
            _strokeWidth = w;
    }

    private async void ClearDrawBtn_Click(object sender, RoutedEventArgs e)
    {
        LocalAnnotationCanvas.Children.Clear();
        await _session.SendDrawAsync(new DrawPayload { Action = "clear" });
    }

    private async void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingMonitors || _sessionClosed) return;
        if (MonitorCombo.SelectedItem is MonitorInfoDto m)
        {
            LocalAnnotationCanvas.Children.Clear();
            await _session.SendSetMonitorAsync(m.Index);
        }
    }

    private async void CtrlAltDelBtn_Click(object sender, RoutedEventArgs e) =>
        await _session.SendSpecialAsync("ctrl_alt_del");

    private void FullscreenBtn_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void ToggleToolbarBtn_Click(object sender, RoutedEventArgs e)
    {
        _toolbarExpanded = !_toolbarExpanded;
        ToolbarExpandedPanel.Visibility = _toolbarExpanded ? Visibility.Visible : Visibility.Collapsed;
        ToolbarCollapsedBar.Visibility = _toolbarExpanded ? Visibility.Collapsed : Visibility.Visible;
        if (ToggleToolbarBtn is not null)
            ToggleToolbarBtn.Content = _toolbarExpanded ? "▲ Ocultar" : "▼ Ferramentas";
    }

    private async void EndSessionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionClosed) return;
        await _session.SendEndSessionAsync();
        Close();
    }

    private void StallTimer_Tick(object? sender, EventArgs e)
    {
        if (_sessionClosed) return;
        var idle = DateTime.UtcNow - _lastFrameUtc;
        if (idle > TimeSpan.FromSeconds(60) && !_session.IsConnected)
            ShowDisconnected("Sem resposta do dispositivo remoto.");
    }

    private void OnSessionDisconnected(string reason) =>
        Dispatcher.Invoke(() => ShowDisconnected(reason));

    private void ShowDisconnected(string reason)
    {
        if (_sessionClosed) return;
        _sessionClosed = true;
        _stallTimer.Stop();
        ReleaseCapture();
        DisconnectReasonText.Text = reason;
        DisconnectedOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Conexão encerrada";
    }

    private void CloseSessionBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void OnClosedCleanup(object? sender, EventArgs e)
    {
        _stallTimer.Stop();
        _keyboardHook.Dispose();
        ReleaseCapture();
    }

    private void ReleaseCapture()
    {
        if (_isDragging || _isDrawing)
        {
            VideoViewbox.ReleaseMouseCapture();
            _isDragging = false;
            _isDrawing = false;
        }
        RemovePreviewShape();
    }

    private bool IsRemoteFocused()
    {
        var fg = GetForegroundWindow();
        return fg == _windowHandle || IsChild(_windowHandle, fg);
    }

    private bool ShouldInterceptKey(int vk, bool down) =>
        !_sessionClosed && _tool == DrawTool.Pointer && IsRemoteFocused();

    private bool OnHookKey(int vk, bool down)
    {
        if (_sessionClosed) return false;
        _ = _session.SendKeyVkAsync(vk, down);
        return true;
    }

    private void ActivateAndFocus()
    {
        Activate();
        Focus();
        Keyboard.Focus(this);
    }

    private void OnFrameReceived(byte[] jpeg)
    {
        _lastFrameUtc = DateTime.UtcNow;
        Dispatcher.Invoke(() =>
        {
            using var ms = new MemoryStream(jpeg);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            _frameWidth = bitmap.PixelWidth;
            _frameHeight = bitmap.PixelHeight;
            RemoteImage.Source = bitmap;
            RemoteImage.Width = _frameWidth;
            RemoteImage.Height = _frameHeight;
        });
    }

    private (int x, int y, bool valid) GetFrameCoords(MouseEventArgs e)
    {
        if (_sessionClosed || _frameWidth <= 0 || _frameHeight <= 0)
            return (0, 0, false);

        var pos = e.GetPosition(VideoViewbox);
        var viewW = VideoViewbox.ActualWidth;
        var viewH = VideoViewbox.ActualHeight;
        if (viewW <= 0 || viewH <= 0) return (0, 0, false);

        var scale = Math.Min(viewW / _frameWidth, viewH / _frameHeight);
        var renderedW = _frameWidth * scale;
        var renderedH = _frameHeight * scale;
        var offsetX = (viewW - renderedW) / 2.0;
        var offsetY = (viewH - renderedH) / 2.0;

        var frameX = (pos.X - offsetX) / scale;
        var frameY = (pos.Y - offsetY) / scale;

        if (frameX < 0 || frameY < 0 || frameX >= _frameWidth || frameY >= _frameHeight)
            return (0, 0, false);

        return ((int)Math.Round(frameX), (int)Math.Round(frameY), true);
    }

    private void VideoArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_sessionClosed) return;
        var (x, y, valid) = GetFrameCoords(e);
        if (!valid) return;

        if (_tool == DrawTool.Pointer)
            _session.SendMouseMove(x, y);

        if (_isDrawing && _tool == DrawTool.Pen)
        {
            _strokePoints.Add(new DrawPointDto { X = x, Y = y });
            if (_strokePoints.Count >= 2)
                DrawLocalLine(_strokePoints[^2], _strokePoints[^1]);
        }
        else if (_isDrawing && _tool is DrawTool.Rect or DrawTool.Ellipse or DrawTool.Arrow)
            UpdatePreviewShape(x, y);
    }

    private async void VideoArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_sessionClosed) return;
        ActivateAndFocus();
        var (x, y, valid) = GetFrameCoords(e);
        if (!valid) return;

        if (_tool != DrawTool.Pointer)
        {
            _isDrawing = true;
            VideoViewbox.CaptureMouse();
            _shapeStartX = x;
            _shapeStartY = y;
            if (_tool == DrawTool.Pen)
            {
                _strokePoints.Clear();
                _strokePoints.Add(new DrawPointDto { X = x, Y = y });
            }
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount >= 2)
            {
                await _session.SendMouseAsync(x, y, "double");
                return;
            }
            _isDragging = true;
            VideoViewbox.CaptureMouse();
            await _session.SendMouseAsync(x, y, "left_down");
        }
        else if (e.ChangedButton == MouseButton.Right)
            await _session.SendMouseAsync(x, y, "right_down");
    }

    private async void VideoArea_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_sessionClosed) return;
        var (x, y, valid) = GetFrameCoords(e);
        if (!valid) return;

        if (_isDrawing && _tool != DrawTool.Pointer)
        {
            _isDrawing = false;
            VideoViewbox.ReleaseMouseCapture();
            RemovePreviewShape();

            if (_tool == DrawTool.Pen && _strokePoints.Count >= 2)
            {
                await _session.SendDrawAsync(new DrawPayload
                {
                    Action = "stroke",
                    Color = _drawColor,
                    Width = _strokeWidth,
                    Points = _strokePoints.ToList(),
                });
            }
            else if (_tool is DrawTool.Rect or DrawTool.Ellipse or DrawTool.Arrow)
            {
                var action = _tool switch
                {
                    DrawTool.Rect => "rect",
                    DrawTool.Ellipse => "ellipse",
                    _ => "arrow",
                };
                await _session.SendDrawAsync(new DrawPayload
                {
                    Action = action,
                    Color = _drawColor,
                    Width = _strokeWidth,
                    X1 = _shapeStartX,
                    Y1 = _shapeStartY,
                    X2 = x,
                    Y2 = y,
                });
                DrawLocalShape(action, _shapeStartX, _shapeStartY, x, y);
            }
            return;
        }

        if (e.ChangedButton == MouseButton.Left && _isDragging)
        {
            _isDragging = false;
            VideoViewbox.ReleaseMouseCapture();
            await _session.SendMouseAsync(x, y, "left_up");
        }
        else if (e.ChangedButton == MouseButton.Right)
            await _session.SendMouseAsync(x, y, "right_up");
    }

    private void DrawLocalLine(DrawPointDto a, DrawPointDto b)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(_drawColor)!;
        LocalAnnotationCanvas.Children.Add(new Line
        {
            X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
            Stroke = brush, StrokeThickness = _strokeWidth,
        });
    }

    private void DrawLocalShape(string action, int x1, int y1, int x2, int y2)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(_drawColor)!;
        var pen = new Pen(brush, _strokeWidth);
        if (action == "rect")
        {
            var r = new Rectangle
            {
                Width = Math.Abs(x2 - x1), Height = Math.Abs(y2 - y1),
                Stroke = pen.Brush, StrokeThickness = pen.Thickness,
            };
            Canvas.SetLeft(r, Math.Min(x1, x2));
            Canvas.SetTop(r, Math.Min(y1, y2));
            LocalAnnotationCanvas.Children.Add(r);
        }
        else if (action == "ellipse")
        {
            var el = new Ellipse
            {
                Width = Math.Abs(x2 - x1), Height = Math.Abs(y2 - y1),
                Stroke = pen.Brush, StrokeThickness = pen.Thickness,
            };
            Canvas.SetLeft(el, Math.Min(x1, x2));
            Canvas.SetTop(el, Math.Min(y1, y2));
            LocalAnnotationCanvas.Children.Add(el);
        }
        else
        {
            LocalAnnotationCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = pen.Brush, StrokeThickness = pen.Thickness,
            });
        }
    }

    private void UpdatePreviewShape(int x, int y)
    {
        RemovePreviewShape();
        var brush = (Brush)new BrushConverter().ConvertFromString(_drawColor)!;
        _previewShape = _tool switch
        {
            DrawTool.Rect => new Rectangle { Stroke = brush, StrokeThickness = _strokeWidth },
            DrawTool.Ellipse => new Ellipse { Stroke = brush, StrokeThickness = _strokeWidth },
            _ => new Line { X1 = _shapeStartX, Y1 = _shapeStartY, Stroke = brush, StrokeThickness = _strokeWidth },
        };

        if (_previewShape is Line ln)
        {
            ln.X2 = x;
            ln.Y2 = y;
        }
        else
        {
            _previewShape.Width = Math.Abs(x - _shapeStartX);
            _previewShape.Height = Math.Abs(y - _shapeStartY);
            Canvas.SetLeft(_previewShape, Math.Min(_shapeStartX, x));
            Canvas.SetTop(_previewShape, Math.Min(_shapeStartY, y));
        }

        LocalAnnotationCanvas.Children.Add(_previewShape);
    }

    private void RemovePreviewShape()
    {
        if (_previewShape is null) return;
        LocalAnnotationCanvas.Children.Remove(_previewShape);
        _previewShape = null;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _stallTimer.Stop();
        if (!_sessionClosed)
            await _session.SendEndSessionAsync();
        await _session.DisposeAsync();
        base.OnClosed(e);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
}
