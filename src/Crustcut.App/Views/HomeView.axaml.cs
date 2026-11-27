using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;
using Crustcut.Presentation.Navigation;

namespace Crustcut.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        PageActivity.Track(this, active => (DataContext as HomeViewModel)?.SetViewing(active));
        // Links to pages that don't exist on this platform are hidden, not dead.
        MemoryLink.IsVisible = NavCatalog.IsKnown("Memory");
        CleanupLink.IsVisible = NavCatalog.IsKnown("Cleanup");
        DiagnosisLink.IsVisible = NavCatalog.IsKnown("Diagnosis");
    }

    private async void FreeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm) await vm.FreeMemoryAsync();
    }

    private async void ItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string action }) return;
        if (action == "optimize")
        {
            if (DataContext is HomeViewModel vm) await vm.OptimizeAsync();
            return;
        }
        Go(action);
    }

    private void GoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string id }) Go(id);
    }

    private void Go(string id)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow shell) shell.NavigateTo(id);
    }
}
