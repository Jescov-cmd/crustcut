using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class MemoryView : UserControl
{
    // Refreshes the "using the most memory" list while the page is visible; stopped on
    // detach so a background tab never pays for process enumeration.
    private readonly Avalonia.Threading.DispatcherTimer _topMemTimer =
        new() { Interval = TimeSpan.FromSeconds(5) };

    public MemoryView()
    {
        InitializeComponent();
        _topMemTimer.Tick += async (_, _) =>
        {
            if (DataContext is MemoryPriorityViewModel vm) await vm.RefreshTopMemoryAsync();
        };
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Proof-of-life for the automatic cleanup, refreshed on every visit.
        LastCleanupText.Text = MemoryPriorityViewModel.LastCleanupText();
        if (DataContext is MemoryPriorityViewModel vm) _ = vm.RefreshTopMemoryAsync();
        _topMemTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topMemTimer.Stop();
    }

    private async void CleanNowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        CleanNowButton.IsEnabled = false;
        StatusText.Text = "Cleaning up…";
        try { StatusText.Text = await vm.CleanNowAsync(); }
        finally { CleanNowButton.IsEnabled = true; }
    }

    private async void DeepCleanClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        DeepCleanButton.IsEnabled = false;
        StatusText.Text = "Deep cleaning…";
        try { StatusText.Text = await vm.DeepCleanAsync(); }
        finally { DeepCleanButton.IsEnabled = true; }
    }

    private async void RecommendedLimitsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MemoryPriorityViewModel vm) return;
        StatusText.Text = "Measuring running apps…";
        StatusText.Text = await vm.ApplyRecommendedLimitsAsync();
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
