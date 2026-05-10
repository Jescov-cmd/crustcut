using System;
using System.Windows;
using System.Windows.Controls;
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
        var menu = new ContextMenu();
        var show = new MenuItem { Header = "Show PrimeOS Tuner" };
        show.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        var optimize = new MenuItem { Header = "Optimize Now" };
        optimize.Click += (_, _) => OptimizeRequested?.Invoke(this, EventArgs.Empty);
        var sep = new Separator();
        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(show);
        menu.Items.Add(optimize);
        menu.Items.Add(sep);
        menu.Items.Add(exit);
        return menu;
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
        public event EventHandler? CanExecuteChanged;
    }
}
