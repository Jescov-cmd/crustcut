using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

/// <summary>
/// The in-game performance overlay window. Transparent + always-on-top + click-through, so
/// it floats over the game without a background or stealing input. A global hotkey
/// (Ctrl+Shift+O) toggles "edit mode": the window becomes interactive (and shows a subtle
/// outline) so it can be dragged to a new position, then locked again.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x0B0B;
    private const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000;
    private const uint VK_O = 0x4F;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private readonly OverlayViewModel _vm;
    private IntPtr _hwnd;
    private bool _editMode;

    /// <summary>Raised with the new (Left, Top) when the user finishes moving the overlay.</summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>Raised when the user leaves edit mode (locks the overlay). Lets the owner
    /// re-evaluate visibility so a reposition done on the desktop doesn't leave the overlay
    /// stuck on screen when no game is running.</summary>
    public event Action? EditModeEnded;

    public OverlayWindow(OverlayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyExStyle(editMode: false);

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        try { RegisterHotKey(_hwnd, HotkeyId, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O); }
        catch { /* hotkey unavailable — overlay still works, just no edit toggle */ }
    }

    private void ApplyExStyle(bool editMode)
    {
        if (_hwnd == IntPtr.Zero) return;
        var ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        ex |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        if (editMode)
        {
            ex &= ~WS_EX_TRANSPARENT;   // interactive: capture mouse for dragging
            ex &= ~WS_EX_NOACTIVATE;
        }
        else
        {
            ex |= WS_EX_TRANSPARENT;    // click-through: input passes to the game
            ex |= WS_EX_NOACTIVATE;     // never steal focus from the game
        }
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ToggleEditMode();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void ToggleEditMode() => SetEditMode(!_editMode);

    public void SetEditMode(bool on)
    {
        _editMode = on;
        _vm.EditMode = on;
        ApplyExStyle(editMode: on);
        if (on)
        {
            Topmost = true;
            Activate();
        }
        else
        {
            PositionChanged?.Invoke(Left, Top);   // persist on lock
            EditModeEnded?.Invoke();              // owner re-checks if it should stay visible
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_editMode) return;
        try { DragMove(); } catch { /* mouse already released */ }
        PositionChanged?.Invoke(Left, Top);
    }

    protected override void OnClosed(EventArgs e)
    {
        try { if (_hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, HotkeyId); } catch { }
        base.OnClosed(e);
    }
}
