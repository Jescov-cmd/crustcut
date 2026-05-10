using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class GameBoostView : UserControl
{
    private readonly GameBoostViewModel _vm;

    public GameBoostView(GameBoostViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void BasicToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true })
        {
            ClearOtherToggles(BasicToggle);
            await _vm.ApplyBasicAsync();
        }
    }

    private async void PerformanceToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true })
        {
            ClearOtherToggles(PerformanceToggle);
            await _vm.ApplyPerformanceAsync();
        }
    }

    private async void CustomToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true })
        {
            ClearOtherToggles(CustomToggle);
            await _vm.ApplyCustomAsync();
        }
    }

    private void ClearOtherToggles(ToggleButton keep)
    {
        foreach (var t in new[] { BasicToggle, PerformanceToggle, CustomToggle })
            if (t != keep) t.IsChecked = false;
    }
}
