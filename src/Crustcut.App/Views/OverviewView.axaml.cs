using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class OverviewView : UserControl
{
    public OverviewView() => InitializeComponent();

    private async void OptimizeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OverviewViewModel vm) await vm.RunOneClickAsync();
    }

    private void HistoryClick(object? sender, RoutedEventArgs e)
    {
        // Walk up to the shell window and switch tabs — the page itself has no nav state.
        if (VisualRoot is MainWindow shell) shell.NavigateTo("History");
    }
}
