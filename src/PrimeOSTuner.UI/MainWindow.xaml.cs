using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.Services;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly ShellViewModel _shellVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly SentinelViewModel _sentinelVm;
    private Dictionary<string, Button>? _tabs;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm, SettingsViewModel settingsVm, SentinelViewModel sentinelVm)
    {
        InitializeComponent();
        // Bread loaf icon — shows in taskbar, Alt-Tab, Task Manager Apps tab.
        Icon = BreadIcon.BuildWpfImageSource(64);
        _shellVm = vm;
        _settingsVm = settingsVm;
        _sentinelVm = sentinelVm;
        DataContext = vm;
        var watcher = (FrameworkElement)FindName("WatcherStatusText");
        if (watcher?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        Closing += OnClosing;
        StateChanged += OnStateChanged;

        _tabs = new Dictionary<string, Button>
        {
            ["Dashboard"]   = NavDashboard,
            ["Optimize"]    = NavOptimize,
            ["GameBoost"]   = NavGameBoost,
            ["GameLibrary"] = NavGameLibrary,
            ["Bloatware"]   = NavBloatware,
            ["MemoryPriority"] = NavMemoryPriority,
            ["Optimization101"] = NavOptimization101,
            ["History"]     = NavHistory,
            ["Sentinel"]    = NavSentinel,
            ["Settings"]    = NavSettings,
        };

        SentinelDot.DataContext = _sentinelVm;

        ShowTab("Dashboard");
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Real exit (tray menu Exit) sets a flag on App; honor it.
        if (Application.Current is App app && app.IsExitingForReal) return;

        if (_settingsVm.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int useDark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch { }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowTab(tab);
    }

    private void TabsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (MaxBtn?.Template?.FindName("MaxIcon", MaxBtn) is System.Windows.Shapes.Path maxIcon &&
            MaxBtn.Template.FindName("RestoreIcon", MaxBtn) is System.Windows.Shapes.Path restoreIcon)
        {
            var maximized = WindowState == WindowState.Maximized;
            maxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
            restoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
            MaxBtn.ToolTip = maximized ? "Restore" : "Maximize";
        }
    }

    private void ShowTab(string tab)
    {
        _shellVm.NavigateCommand.Execute(tab);

        if (_tabs != null)
        {
            var activeStyle = (Style)FindResource("TopTabActive");
            var inactiveStyle = (Style)FindResource("TopTab");
            foreach (var (key, btn) in _tabs)
                btn.Style = key == tab ? activeStyle : inactiveStyle;
        }

        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "Bloatware"    => sp.GetRequiredService<BloatwareView>(),
            "MemoryPriority" => sp.GetRequiredService<MemoryPriorityView>(),
            "Optimization101" => sp.GetRequiredService<Optimization101View>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
            "Sentinel"     => sp.GetRequiredService<SentinelView>(),
            "Settings"     => sp.GetRequiredService<SettingsView>(),
            _ => new TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22
            }
        };

        var slide = new TranslateTransform(0, 12);
        PageHost.RenderTransform = slide;
        PageHost.Opacity = 0;
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 12, To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
