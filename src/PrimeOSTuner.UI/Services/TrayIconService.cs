using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using DColor = System.Drawing.Color;
using DFont = System.Drawing.Font;
using DFontStyle = System.Drawing.FontStyle;
using DGraphics = System.Drawing.Graphics;
using DPoint = System.Drawing.Point;
using DBitmap = System.Drawing.Bitmap;
using DGraphicsUnit = System.Drawing.GraphicsUnit;
using DSolidBrush = System.Drawing.SolidBrush;
using DSmoothingMode = System.Drawing.Drawing2D.SmoothingMode;
using DLinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using DIcon = System.Drawing.Icon;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Owns the application's system-tray icon and the right-click menu (Show, Optimize, Exit).
/// Singleton. Constructed once at app startup; disposed on real exit.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private bool _disposed;

    public event EventHandler? ShowRequested;
    public event EventHandler? OptimizeRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "PrimeOS Tuner",
            Icon = BuildIcon(),
            ContextMenu = BuildContextMenu(),
            NoLeftClickDelay = true,
        };
        _icon.LeftClickCommand = new DelegateCommand(() => ShowRequested?.Invoke(this, EventArgs.Empty));
        _icon.ForceCreate();
    }

    public void ShowNotification(string title, string message)
    {
        if (_disposed) return;
        try
        {
            _icon.ShowNotification(title: title, message: message);
        }
        catch
        {
            // Toast can fail on locked-down systems / when the icon hasn't fully registered.
            // Silent failure is OK — notifications are non-critical.
        }
    }

    private ContextMenu BuildContextMenu()
    {
        // Explicit colors so the popup can't fall into white-on-white (the menu
        // renders outside MainWindow, so it doesn't inherit our normal theme).
        var menuBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22));
        var menuFg = Brushes.White;

        var menu = new ContextMenu { Background = menuBg, Foreground = menuFg };
        menu.Items.Add(BuildItem("Show PrimeOS Tuner", menuFg, () => ShowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(BuildItem("Optimize Now", menuFg, () => OptimizeRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildItem("Exit", menuFg, () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        return menu;
    }

    private static MenuItem BuildItem(string header, Brush foreground, Action onClick)
    {
        var item = new MenuItem { Header = header, Foreground = foreground };
        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>
    /// Generates a 32x32 icon at runtime — pink-orange gradient circle with a "P" — so we
    /// don't have to ship a .ico file. GDI Bitmap → Icon via the standard GetHicon trick.
    /// </summary>
    private static DIcon BuildIcon()
    {
        const int sz = 32;
        using var bmp = new DBitmap(sz, sz);
        using (var g = DGraphics.FromImage(bmp))
        {
            g.SmoothingMode = DSmoothingMode.AntiAlias;
            using var brush = new DLinearGradientBrush(
                new DPoint(0, 0), new DPoint(sz, sz),
                DColor.FromArgb(0xF7, 0x41, 0x6B),  // pink
                DColor.FromArgb(0xFF, 0xA0, 0x1F)); // orange
            g.FillEllipse(brush, 0, 0, sz - 1, sz - 1);
            using var font = new DFont("Segoe UI", 16, DFontStyle.Bold, DGraphicsUnit.Pixel);
            using var textBrush = new DSolidBrush(DColor.White);
            const string text = "P";
            var measured = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush,
                (sz - measured.Width) / 2,
                (sz - measured.Height) / 2);
        }
        var hIcon = bmp.GetHicon();
        var icon = (DIcon)DIcon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Dispose();
    }

    private sealed class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public DelegateCommand(Action action) => _action = action;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _action();
        // CanExecute is constant true — this command never disables. Required by ICommand.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
