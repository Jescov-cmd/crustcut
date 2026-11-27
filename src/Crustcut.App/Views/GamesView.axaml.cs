using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation.Navigation;

namespace Crustcut.App.Views;

public partial class GamesView : UserControl
{
    public GamesView()
    {
        InitializeComponent();
        SessionsButton.IsVisible = NavCatalog.IsKnown("Sessions");
    }

    private void SessionsClick(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is MainWindow shell) shell.NavigateTo("Sessions");
    }
}
