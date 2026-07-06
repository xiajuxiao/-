using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace TranslatorTool.Services;

public static class HotkeyNames
{
    public const string ScreenshotTranslation = "Alt+Q";
}

public sealed class HotkeyPressedEventArgs(string hotkeyName) : EventArgs
{
    public string HotkeyName { get; } = hotkeyName;
}

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ScreenshotHotkeyId = 1001;
    private const uint ModAlt = 0x0001;
    private HwndSource? _source;
    private IntPtr _windowHandle;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        if (!RegisterHotKey(_windowHandle, ScreenshotHotkeyId, ModAlt, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.Q)))
        {
            throw new InvalidOperationException("注册 Alt + Q 快捷键失败，可能已被其他程序占用。");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == ScreenshotHotkeyId)
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(HotkeyNames.ScreenshotTranslation));
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, ScreenshotHotkeyId);
            _windowHandle = IntPtr.Zero;
        }

        _source?.RemoveHook(WndProc);
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
