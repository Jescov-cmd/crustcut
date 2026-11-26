using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class GameBoostView : UserControl
{
    public GameBoostView()
    {
        InitializeComponent();
        // Fresh status and a fresh list of running apps each time the page is shown.
        PageActivity.Track(this, active =>
        {
            if (!active || DataContext is not GameBoostViewModel vm) return;
            vm.RefreshGameOnly();
            _ = vm.RefreshCandidatesAsync();
        });
    }

    private async void AddGameOnlyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GameBoostViewModel vm) return;
        await vm.AddSelectedAsync();
        vm.ShowAddPicker = false;
    }

    private void ShowPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameBoostViewModel vm) vm.ShowAddPicker = true;
    }

    private async void RemoveGameOnlyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is GameBoostViewModel vm)
            await vm.RemoveAsync(name);
    }

    private async void WakeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is GameBoostViewModel vm)
            await vm.WakeAsync(name);
    }

    private async void BasicClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameBoostViewModel vm) await vm.ApplyBasicAsync();
    }

    private async void PerformanceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameBoostViewModel vm) await vm.ApplyPerformanceAsync();
    }

    private async void AggressiveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameBoostViewModel vm) await vm.ApplyAggressiveAsync();
    }
}
