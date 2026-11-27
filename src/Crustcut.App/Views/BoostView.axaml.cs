using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class BoostView : UserControl
{
    public BoostView()
    {
        InitializeComponent();
        // Settings can change on All settings or in History while this page is cached;
        // re-read them every time it comes back on screen.
        PageActivity.Track(this, active =>
        {
            if (active && DataContext is BoostViewModel { IsLoaded: true } vm) vm.Sync();
        });
    }

    private async void GroupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { Tag: string key } sw || DataContext is not BoostViewModel vm) return;
        var group = key == "online" ? vm.Online : vm.LowLag;
        await group.SetAsync(sw.IsChecked == true);
        sw.IsChecked = group.IsOn;   // a refused or failed setting snaps the switch back
    }

    private void GoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string id } && TopLevel.GetTopLevel(this) is MainWindow shell) shell.NavigateTo(id);
    }
}
