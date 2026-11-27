using Avalonia.Controls;

namespace Crustcut.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        HistoryLink.IsVisible = Crustcut.Presentation.Navigation.NavCatalog.IsKnown("History");
        GuidesLink.IsVisible = Crustcut.Presentation.Navigation.NavCatalog.IsKnown("Guides");
    }

    private void GoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: string id } && VisualRoot is MainWindow shell) shell.NavigateTo(id);
    }

    private void RepositionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Crustcut.Presentation.SettingsViewModel vm) vm.RepositionOverlay();
    }
}
