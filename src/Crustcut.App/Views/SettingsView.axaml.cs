using Avalonia.Controls;

namespace Crustcut.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void RepositionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Crustcut.Presentation.SettingsViewModel vm) vm.RepositionOverlay();
    }
}
