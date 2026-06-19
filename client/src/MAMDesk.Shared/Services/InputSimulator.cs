using System.Runtime.InteropServices;
using MAMDesk.Shared.Models;

namespace MAMDesk.Shared.Services;

public static class InputSimulator
{
    public static void Apply(InputPayload input, Size nativeScreen, Size frameSize)
    {
        if (input.Tipo == "special")
        {
            ApplySpecial(input);
            return;
        }

        if (input.Tipo == "mouse")
            ApplyMouse(input, nativeScreen, frameSize);
        else if (input.Tipo == "keyboard")
            ApplyKeyboard(input);
    }

    private static void ApplySpecial(InputPayload input)
    {
        if (input.Key == "ctrl_alt_del")
            SendCtrlAltDel();
    }

    private static void SendCtrlAltDel()
    {
        keybd_event(0x11, 0, 0, 0); // Ctrl down
        keybd_event(0x12, 0, 0, 0); // Alt down
        keybd_event(0x2E, 0, 0, 0); // Del down
        keybd_event(0x2E, 0, 2, 0);
        keybd_event(0x12, 0, 2, 0);
        keybd_event(0x11, 0, 2, 0);
    }

    private static void ApplyMouse(InputPayload input, Size nativeScreen, Size frameSize)
    {
        var x = (int)Math.Round(input.X * nativeScreen.Width / (double)Math.Max(1, frameSize.Width));
        var y = (int)Math.Round(input.Y * nativeScreen.Height / (double)Math.Max(1, frameSize.Height));

        x = Math.Clamp(x, 0, nativeScreen.Width - 1);
        y = Math.Clamp(y, 0, nativeScreen.Height - 1);

        SetCursorPos(x, y);

        switch (input.Click)
        {
            case "left_down":
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                break;
            case "left_up":
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                break;
            case "right_down":
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                break;
            case "right_up":
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                break;
            case "left":
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                break;
            case "right":
                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                break;
            case "double":
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                break;
        }
    }

    private static void ApplyKeyboard(InputPayload input)
    {
        var down = input.Down ?? true;

        if (input.Vk is int vk and > 0)
        {
            SendVk(vk, down);
            return;
        }

        if (!string.IsNullOrEmpty(input.Key) && input.Key.Length == 1)
        {
            var scan = VkKeyScan(input.Key[0]);
            SendVk(scan & 0xFF, down);
        }
    }

    private static void SendVk(int vk, bool down)
    {
        var flags = down ? 0u : KEYEVENTF_KEYUP;
        if (IsExtendedKey(vk))
            flags |= KEYEVENTF_EXTENDEDKEY;

        var inp = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                },
            },
        };

        if (SendInput(1, [inp], InputSize) == 0)
            keybd_event((byte)vk, 0, (int)flags, 0);
    }

    private static bool IsExtendedKey(int vk) => vk switch
    {
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 => true,
        0x2D or 0x2E => true,
        0x5B or 0x5C or 0x5D => true,
        0x6F => true,
        0xA3 or 0xA5 => true,
        _ => false,
    };

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const int MOUSEEVENTF_LEFTUP = 0x0004;
    private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const int MOUSEEVENTF_RIGHTUP = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
