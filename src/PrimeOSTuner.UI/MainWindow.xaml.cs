using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
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
    private Dictionary<string, Button>? _tabs;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm, SettingsViewModel settingsVm)
    {
        InitializeComponent();
        _shellVm = vm;
        _settingsVm = settingsVm;
        DataContext = vm;
        var watcher = (FrameworkElement)FindName("WatcherStatusText");
        if (watcher?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        Closing += OnClosing;

        _tabs = new Dictionary<string, Button>
        {
            ["Dashboard"]   = NavDashboard,
            ["Optimize"]    = NavOptimize,
            ["GameBoost"]   = NavGameBoost,
            ["GameLibrary"] = NavGameLibrary,
            ["Bloatware"]   = NavBloatware,
            ["MemoryPriority"] = NavMemoryPriority,
            ["History"]     = NavHistory,
            ["Settings"]    = NavSettings,
        };

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
            "History"      => sp.GetRequiredService<HistoryView>(),
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
