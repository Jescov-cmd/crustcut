using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class CleanupView : UserControl
{
    public CleanupView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Startup entries change from outside (installers, Task Manager) — reload per visit.
        if (DataContext is CleanupViewModel vm) _ = vm.LoadStartupAppsAsync();
    }

    private async void StartupToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { Tag: CleanupViewModel.StartupRowVm row } sw &&
            DataContext is CleanupViewModel vm)
            await vm.SetStartupEnabledAsync(row, sw.IsChecked == true);
    }

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
