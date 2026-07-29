using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class GameBoostView : UserControl
{
    public GameBoostView() => InitializeComponent();

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
