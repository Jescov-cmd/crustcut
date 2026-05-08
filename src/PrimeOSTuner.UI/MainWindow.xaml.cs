using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm)
    {
        InitializeComponent();
        DataContext = vm;
        var bottomBlock = (FrameworkElement)FindName("WatcherStatusText");
        if (bottomBlock?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        ShowTab("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowTab(tab);
    }

    private void ShowTab(string tab)
    {
        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "CustomMode"   => sp.GetRequiredService<CustomModeView>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
            _ => new TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22,
                Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
            }
        };
    }
}
