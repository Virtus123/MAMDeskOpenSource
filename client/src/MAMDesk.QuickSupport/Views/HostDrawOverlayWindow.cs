using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MAMDesk.Shared.Models;
using MAMDesk.Shared.Services;

namespace MAMDesk.QuickSupport.Views;

public sealed class HostDrawOverlayWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly ScreenCaptureService _capture;

    public HostDrawOverlayWindow(ScreenCaptureService capture)
    {
        _capture = capture;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = _canvas;
        SyncBounds();
    }

    public void SyncBounds()
    {
        var bounds = _capture.GetNativeBounds();
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    public void Clear() => _canvas.Children.Clear();

    public void ApplyDraw(DrawPayload draw)
    {
        var frame = _capture.GetCaptureFrameSize();
        var native = _capture.GetNativeScreenSize();
        var color = ParseColor(draw.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1, draw.Width));

        switch (draw.Action)
        {
            case "stroke":
                if (draw.Points is null || draw.Points.Count < 2) return;
                for (var i = 1; i < draw.Points.Count; i++)
                {
                    _canvas.Children.Add(new Line
                    {
                        X1 = ScaleX(draw.Points[i - 1].X, frame.Width, native.Width),
                        Y1 = ScaleY(draw.Points[i - 1].Y, frame.Height, native.Height),
                        X2 = ScaleX(draw.Points[i].X, frame.Width, native.Width),
                        Y2 = ScaleY(draw.Points[i].Y, frame.Height, native.Height),
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    });
                }
                break;
            case "rect":
                if (!draw.X1.HasValue || !draw.Y1.HasValue || !draw.X2.HasValue || !draw.Y2.HasValue) return;
                _canvas.Children.Add(new Rectangle
                {
                    Width = Math.Abs(ScaleX(draw.X2.Value, frame.Width, native.Width) - ScaleX(draw.X1.Value, frame.Width, native.Width)),
                    Height = Math.Abs(ScaleY(draw.Y2.Value, frame.Height, native.Height) - ScaleY(draw.Y1.Value, frame.Height, native.Height)),
                    Stroke = pen.Brush,
                    StrokeThickness = pen.Thickness,
                    Fill = Brushes.Transparent,
                });
                Canvas.SetLeft(_canvas.Children[^1], Math.Min(ScaleX(draw.X1.Value, frame.Width, native.Width), ScaleX(draw.X2.Value, frame.Width, native.Width)));
                Canvas.SetTop(_canvas.Children[^1], Math.Min(ScaleY(draw.Y1.Value, frame.Height, native.Height), ScaleY(draw.Y2.Value, frame.Height, native.Height)));
                break;
            case "ellipse":
                if (!draw.X1.HasValue || !draw.Y1.HasValue || !draw.X2.HasValue || !draw.Y2.HasValue) return;
                var el = new Ellipse
                {
                    Width = Math.Abs(ScaleX(draw.X2.Value, frame.Width, native.Width) - ScaleX(draw.X1.Value, frame.Width, native.Width)),
                    Height = Math.Abs(ScaleY(draw.Y2.Value, frame.Height, native.Height) - ScaleY(draw.Y1.Value, frame.Height, native.Height)),
                    Stroke = pen.Brush,
                    StrokeThickness = pen.Thickness,
                    Fill = Brushes.Transparent,
                };
                _canvas.Children.Add(el);
                Canvas.SetLeft(el, Math.Min(ScaleX(draw.X1.Value, frame.Width, native.Width), ScaleX(draw.X2.Value, frame.Width, native.Width)));
                Canvas.SetTop(el, Math.Min(ScaleY(draw.Y1.Value, frame.Height, native.Height), ScaleY(draw.Y2.Value, frame.Height, native.Height)));
                break;
            case "arrow":
                if (!draw.X1.HasValue || !draw.Y1.HasValue || !draw.X2.HasValue || !draw.Y2.HasValue) return;
                _canvas.Children.Add(new Line
                {
                    X1 = ScaleX(draw.X1.Value, frame.Width, native.Width),
                    Y1 = ScaleY(draw.Y1.Value, frame.Height, native.Height),
                    X2 = ScaleX(draw.X2.Value, frame.Width, native.Width),
                    Y2 = ScaleY(draw.Y2.Value, frame.Height, native.Height),
                    Stroke = pen.Brush,
                    StrokeThickness = pen.Thickness,
                });
                break;
        }
    }

    private static double ScaleX(int frameX, int frameW, int nativeW) =>
        frameX * nativeW / (double)Math.Max(1, frameW);

    private static double ScaleY(int frameY, int frameH, int nativeH) =>
        frameY * nativeH / (double)Math.Max(1, frameH);

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return Colors.Red;
        }
    }
}
