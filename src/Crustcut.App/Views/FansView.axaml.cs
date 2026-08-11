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
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as FansViewModel)?.Refresh();
        _refresh.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _refresh.Stop();   // no reason to poll sensors while the tab isn't visible
    }

    private void ToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch sw && DataContext is FansViewModel vm)
            vm.Toggle(sw.IsChecked == true);
    }

    private void SilentClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Silent);

    private void BalancedClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Balanced);

    private void PerformanceClick(object? sender, RoutedEventArgs e)
        => (DataContext as FansViewModel)?.SelectMode(FanMode.Performance);
}
