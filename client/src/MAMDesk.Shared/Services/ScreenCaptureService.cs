using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public sealed class ScreenCaptureService : IDisposable
{
    private int _quality;
    private float _scale;
    private int _monitorIndex;
    private readonly ImageCodecInfo _jpegEncoder;
    private readonly EncoderParameters _encoderParams;

    public ScreenCaptureService(int quality = 92, float scale = 0.95f, int monitorIndex = 0)
    {
        _quality = quality;
        _scale = scale;
        _monitorIndex = monitorIndex;
        _jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        _encoderParams = new EncoderParameters(1);
        _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_quality);
    }

    public void UpdateSettings(int quality, float scale)
    {
        _quality = Math.Clamp(quality, 40, 95);
        _scale = Math.Clamp(scale, 0.5f, 1f);
        _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_quality);
    }

    public void SetMonitor(int index)
    {
        var screens = Screen.AllScreens;
        _monitorIndex = index >= 0 && index < screens.Length ? index : 0;
    }

    public int CurrentMonitorIndex => _monitorIndex;

    public IReadOnlyList<MonitorInfoDto> GetMonitors()
    {
        return Screen.AllScreens.Select((s, i) => new MonitorInfoDto
        {
            Index = i,
            Name = s.Primary ? $"Monitor {i + 1} (Principal)" : $"Monitor {i + 1}",
            Width = s.Bounds.Width,
            Height = s.Bounds.Height,
            IsPrimary = s.Primary,
        }).ToList();
    }

    public byte[] CaptureScreenJpeg()
    {
        var bounds = GetCaptureBounds();
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            CursorHelper.DrawOnCapture(g, bounds);
        }

        var targetWidth = Math.Max(1, (int)(bitmap.Width * _scale));
        var targetHeight = Math.Max(1, (int)(bitmap.Height * _scale));

        if (targetWidth == bitmap.Width && targetHeight == bitmap.Height)
        {
            using var ms = new MemoryStream(Math.Max(65536, targetWidth * targetHeight / 4));
            bitmap.Save(ms, _jpegEncoder, _encoderParams);
            return ms.ToArray();
        }

        using var scaled = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = _scale >= 0.9f
                ? System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
                : System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
        }

        using var output = new MemoryStream(Math.Max(65536, targetWidth * targetHeight / 4));
        scaled.Save(output, _jpegEncoder, _encoderParams);
        return output.ToArray();
    }

    public byte[] CapturePrimaryScreenJpeg() => CaptureScreenJpeg();

    public Size GetCaptureFrameSize()
    {
        var bounds = GetCaptureBounds();
        return new Size(
            Math.Max(1, (int)(bounds.Width * _scale)),
            Math.Max(1, (int)(bounds.Height * _scale)));
    }

    public Size GetNativeScreenSize()
    {
        var bounds = GetCaptureBounds();
        return new Size(bounds.Width, bounds.Height);
    }

    public Rectangle GetNativeBounds() => GetCaptureBounds();

    public Size GetPrimaryScreenSize() => GetCaptureFrameSize();

    private Rectangle GetCaptureBounds()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return new Rectangle(0, 0, 1920, 1080);

        var idx = _monitorIndex >= 0 && _monitorIndex < screens.Length ? _monitorIndex : 0;
        return screens[idx].Bounds;
    }

    public void Dispose()
    {
        _encoderParams.Dispose();
    }
}
