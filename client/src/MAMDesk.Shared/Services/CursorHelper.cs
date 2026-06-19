using System.Drawing;
using System.Runtime.InteropServices;

namespace MAMDesk.Shared.Services;

public static class CursorHelper
{
    private const int CursorShowing = 0x0001;
    private const int StdArrow = 32512;
    private const int StdIBeam = 32513;
    private const int StdWait = 32514;
    private const int StdCross = 32515;
    private const int StdHand = 32649;
    private const int StdSizeAll = 32646;
    private const int StdSizeNs = 32645;
    private const int StdSizeWe = 32644;
    private const int StdNo = 32648;
    private const int StdAppStarting = 32650;

    private static readonly IntPtr Arrow = LoadCursor(IntPtr.Zero, (IntPtr)StdArrow);
    private static readonly IntPtr IBeam = LoadCursor(IntPtr.Zero, (IntPtr)StdIBeam);
    private static readonly IntPtr Wait = LoadCursor(IntPtr.Zero, (IntPtr)StdWait);
    private static readonly IntPtr Cross = LoadCursor(IntPtr.Zero, (IntPtr)StdCross);
    private static readonly IntPtr Hand = LoadCursor(IntPtr.Zero, (IntPtr)StdHand);
    private static readonly IntPtr SizeAll = LoadCursor(IntPtr.Zero, (IntPtr)StdSizeAll);
    private static readonly IntPtr SizeNs = LoadCursor(IntPtr.Zero, (IntPtr)StdSizeNs);
    private static readonly IntPtr SizeWe = LoadCursor(IntPtr.Zero, (IntPtr)StdSizeWe);
    private static readonly IntPtr No = LoadCursor(IntPtr.Zero, (IntPtr)StdNo);
    private static readonly IntPtr AppStarting = LoadCursor(IntPtr.Zero, (IntPtr)StdAppStarting);

    public static string GetCurrentStyleName()
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref info) || info.flags != CursorShowing)
            return "arrow";

        return ClassifyHandle(info.hCursor);
    }

    public static void DrawOnCapture(Graphics g, Rectangle monitorBounds)
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref info) || info.flags != CursorShowing)
            return;

        if (info.ptScreenPos.X < monitorBounds.Left || info.ptScreenPos.X >= monitorBounds.Right ||
            info.ptScreenPos.Y < monitorBounds.Top || info.ptScreenPos.Y >= monitorBounds.Bottom)
            return;

        var x = info.ptScreenPos.X - monitorBounds.Left;
        var y = info.ptScreenPos.Y - monitorBounds.Top;

        var hdc = g.GetHdc();
        try
        {
            DrawIcon(hdc, x, y, info.hCursor);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
    }

    private static string ClassifyHandle(IntPtr hCursor)
    {
        if (hCursor == IBeam) return "ibeam";
        if (hCursor == Hand) return "hand";
        if (hCursor == Wait) return "wait";
        if (hCursor == Cross) return "cross";
        if (hCursor == SizeAll) return "sizeall";
        if (hCursor == SizeNs) return "sizens";
        if (hCursor == SizeWe) return "sizewe";
        if (hCursor == No) return "no";
        if (hCursor == AppStarting) return "wait";
        if (hCursor == Arrow) return "arrow";
        return "arrow";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern bool DrawIcon(IntPtr hDC, int x, int y, IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);
}
