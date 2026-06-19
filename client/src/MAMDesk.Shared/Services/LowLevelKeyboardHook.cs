using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MAMDesk.Shared.Services;

/// <summary>
/// Intercepta teclas antes do Windows (Alt+Tab, Win+D, etc.) quando a sessão remota está ativa.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private HookProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private Func<int, bool, bool>? _shouldIntercept;
    private Func<int, bool, bool>? _onKey;

    public void Install(Func<int, bool, bool> shouldIntercept, Func<int, bool, bool> onKey)
    {
        _shouldIntercept = shouldIntercept;
        _onKey = onKey;
        _proc = Callback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _proc = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _shouldIntercept is not null && _onKey is not null)
        {
            var vk = Marshal.ReadInt32(lParam);
            var down = wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSyskeydown;

            if (_shouldIntercept(vk, down))
            {
                _onKey(vk, down);
                return (IntPtr)1; // bloqueia no PC local
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
