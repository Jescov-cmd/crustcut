using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class MemoryView : UserControl
{
    public MemoryView() => InitializeComponent();

    private async void RescanClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        StatusText.Text = "Scanning…";
        var added = await vm.RescanRunningAppsAsync();
        StatusText.Text = added == 0
            ? "No new apps found."
            : $"Added {added} app(s).";
    }

    private async void PriorityChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Persist immediately: a dropdown that forgets its value on tab-switch is worse
        // than no dropdown.
        if (sender is ComboBox { Tag: PriorityRuleVm rule } && DataContext is MemoryPriorityViewModel vm)
            await vm.UpdateRuleAsync(rule);
    }

    private async void ProtectChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: PriorityRuleVm rule } && DataContext is MemoryPriorityViewModel vm)
            await vm.UpdateRuleAsync(rule);
    }

    private async void RemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PriorityRuleVm rule } && DataContext is MemoryPriorityViewModel vm)
            await vm.RemoveAsync(rule);
    }

    private async void RecommendedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        StatusText.Text = "Applying…";
        var (addedCount, updated) = await vm.ApplyRecommendedToAllGamesAsync();
        StatusText.Text = $"{addedCount} added, {updated} updated.";
    }
}
