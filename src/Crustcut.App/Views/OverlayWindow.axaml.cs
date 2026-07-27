using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Crustcut.App.Views;

/// <summary>
/// See-through in-game OSD. Click-through is applied with the same Win32 interop the WPF
/// build used; the approach was validated on Avalonia before the migration — see
/// docs/performance/2026-07-26-avalonia-overlay-spike.md.
/// </summary>
public partial class OverlayWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private bool _editMode;

    public OverlayWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ApplyClickThrough(true);
        PointerPressed += OnPointerPressed;
    }

    /// <summary>
    /// Edit mode drops click-through so the overlay can be dragged, and outlines it so it's
    /// obvious the window is grabbable. Toggling back re-arms click-through.
    /// </summary>
    public bool EditMode
    {
        get => _editMode;
        set
        {
            _editMode = value;
            ApplyClickThrough(!value);
            Root.Background = value ? new SolidColorBrush(Color.Parse("#66000000")) : Brushes.Transparent;
            Root.BorderBrush = new SolidColorBrush(Color.Parse("#E8C088"));
            Root.BorderThickness = new Thickness(value ? 1.5 : 0);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only draggable while editing — otherwise clicks pass straight through to the game.
        if (_editMode) BeginMoveDrag(e);
    }

    private void ApplyClickThrough(bool enabled)
    {
        var handle = TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero) return;

        var style = GetWindowLong(handle.Handle, GWL_EXSTYLE)
                    | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

        style = enabled ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;

        SetWindowLong(handle.Handle, GWL_EXSTYLE, style);
    }
}
