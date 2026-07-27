using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Crustcut.App.Services;

/// <summary>
/// Registers a system-wide hotkey against a window's HWND. Windows-only; the interface is
/// kept narrow so a macOS implementation can slot in later.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004;
    private const int HotkeyId = 0xC0DE;

    private IntPtr _hWnd;
    private bool _registered;

    /// <summary>Registers Ctrl+Shift+O against <paramref name="window"/>. Returns false if taken.</summary>
    public bool Register(Window window, uint virtualKey = 0x4F /* O */)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero) return false;

        _hWnd = handle.Handle;
        _registered = RegisterHotKey(_hWnd, HotkeyId, MOD_CONTROL | MOD_SHIFT, virtualKey);
        return _registered;
    }

    public void Dispose()
    {
        if (!_registered) return;
        try { UnregisterHotKey(_hWnd, HotkeyId); } catch { }
        _registered = false;
    }
}
