using System;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
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
            ToolTipText = "Crustcut",
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
        // Apply our fully-themed templates. Without TrayContextMenu, WPF's default
        // template paints an icon-column gutter on the left that reads as a white strip.
        if (Application.Current?.TryFindResource("TrayContextMenu") is Style menuStyle)
            menu.Style = menuStyle;

        menu.Items.Add(BuildItem("Show Crustcut", () => ShowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(BuildItem("Optimize Now", () => OptimizeRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(BuildSeparator());
        menu.Items.Add(BuildItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        return menu;
    }

    private static MenuItem BuildItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        if (Application.Current?.TryFindResource("TrayMenuItem") is Style trayStyle)
            item.Style = trayStyle;
        item.Click += (_, _) => onClick();
        return item;
    }

    private static Separator BuildSeparator()
    {
        var sep = new Separator();
        if (Application.Current?.TryFindResource("TraySeparator") is Style sepStyle)
            sep.Style = sepStyle;
        return sep;
    }

    private static DIcon BuildIcon() => BreadIcon.BuildSystemIcon(32);

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
