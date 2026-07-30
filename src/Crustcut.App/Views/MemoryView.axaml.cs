using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class MemoryView : UserControl
{
    public MemoryView() => InitializeComponent();

    private async void CleanNowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        CleanNowButton.IsEnabled = false;
        StatusText.Text = "Cleaning up…";
        try { StatusText.Text = await vm.CleanNowAsync(); }
        finally { CleanNowButton.IsEnabled = true; }
    }

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

    private async void MemoryLimitChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Re-binding fires SelectionChanged with a transient NULL selection before the
        // real value is restored — persisting at that moment would silently wipe the
        // user's limit. Only a real selection is worth acting on; UpdateRuleAsync then
        // no-ops unless the value actually differs from what's persisted.
        if (sender is ComboBox { SelectedItem: null }) return;
        if (sender is ComboBox { Tag: PriorityRuleVm rule } && DataContext is MemoryPriorityViewModel vm)
            await vm.UpdateRuleAsync(rule);
    }

    private async void ProtectChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: PriorityRuleVm rule } && DataContext is MemoryPriorityViewModel vm)
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
