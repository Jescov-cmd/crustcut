using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;
using Wpf.Ui.Controls;

namespace PrimeOSTuner.UI;

public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _shellVm;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm)
    {
        InitializeComponent();
        _shellVm = vm;
        DataContext = vm;
        var bottomBlock = (FrameworkElement)FindName("WatcherStatusText");
        if (bottomBlock?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        ShowTab("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string tab }) ShowTab(tab);
    }

    private void ShowTab(string tab)
    {
        _shellVm.NavigateCommand.Execute(tab);
        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "CustomMode"   => sp.GetRequiredService<CustomModeView>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
            _ => new System.Windows.Controls.TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22,
                Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
            }
        };
    }
}
