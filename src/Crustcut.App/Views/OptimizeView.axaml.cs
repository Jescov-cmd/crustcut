using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class OptimizeView : UserControl
{
    public OptimizeView() => InitializeComponent();

    private async void ToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch sw || sw.Tag is not TweakRowVm row) return;
        if (DataContext is not OptimizeViewModel vm) return;

        // IsChecked has already been flipped by the click; that is the state the user wants.
        await vm.ToggleAsync(row, sw.IsChecked == true);
    }
}
