using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    private const double SlotHeight = 44;
    private const double IndicatorOffset = 4;

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

        var idx = _shellVm.SelectedTabIndex;
        var targetTop = idx * SlotHeight + IndicatorOffset;

        NavIndicator.Visibility = Visibility.Visible;
        var anim = new DoubleAnimation
        {
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        Storyboard.SetTarget(anim, NavIndicator);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(Canvas.Top)"));
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }
}
