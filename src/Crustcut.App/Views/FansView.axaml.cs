using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Fans;

namespace Crustcut.App.Views;

public partial class FansView : UserControl
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(2) };

    public FansView()
    {
        InitializeComponent();
        _refresh.Tick += (_, _) => (DataContext as FansViewModel)?.Refresh();
        PageActivity.Track(this, active =>
        {
            var vm = DataContext as FansViewModel;
            if (active)
            {
                vm?.SetViewing(true);
                vm?.Refresh();
                _refresh.Start();
            }
            else
            {
                _refresh.Stop();   // no reason to poll sensors while the tab isn't visible
                vm?.SetViewing(false);
            }
        });
    }

    private void ToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch sw && DataContext is FansViewModel vm)
            vm.Toggle(sw.IsChecked == true);
    }

    private async void CalibrateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FansViewModel vm) await vm.CalibrateAsync();
    }

    private void RpmScaleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is FansViewModel vm)
            vm.CycleRpmScale(name);
    }

    private void AutoClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Auto);

    private void SilentClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Silent);

    private void BalancedClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Balanced);

    private void PerformanceClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Performance);
}
