using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class CleanupView : UserControl
{
    public CleanupView() => InitializeComponent();

    private async void ScanClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CleanupViewModel vm) await vm.RefreshAsync();
    }

    private async void UninstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BloatwareItemRowVm row } && DataContext is CleanupViewModel vm)
            await vm.UninstallAsync(row);
    }

    private async void DisableClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BloatwareItemRowVm row } && DataContext is CleanupViewModel vm)
            await vm.DisableAsync(row);
    }
}
