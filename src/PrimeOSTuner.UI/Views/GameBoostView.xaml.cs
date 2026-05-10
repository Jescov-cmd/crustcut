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

    // Toggle state is bound to GameBoostViewModel.IsBasicActive/IsPerformanceActive/IsAggressiveActive.
    // The VM is a singleton, so the active profile survives tab navigation.
    // Click handlers only need to fire the apply action when the user just turned a toggle ON.

    private async void BasicToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await _vm.ApplyBasicAsync();
    }

    private async void PerformanceToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await _vm.ApplyPerformanceAsync();
    }

    private async void AggressiveToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await _vm.ApplyAggressiveAsync();
    }
}
