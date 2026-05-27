using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class MemoryPriorityView : UserControl
{
    private readonly MemoryPriorityViewModel _vm;
    private readonly GameRegistry _games;
    private bool? _dragSelectValue;  // null = not dragging; true/false = the value to paint while dragging

    public MemoryPriorityView(MemoryPriorityViewModel vm, GameRegistry games)
    {
        InitializeComponent();
        _vm = vm;
        _games = games;
        DataContext = vm;
        Loaded += async (_, _) => { if (_vm.Rules.Count == 0) await _vm.LoadAsync(); ApplyFilter(); };
    }

    private void ApplyFilter()
    {
        var view = _vm.ActiveFilter switch
        {
            "games" => _vm.Rules.Where(r => r.IsGame).ToList(),
            "apps"  => _vm.Rules.Where(r => !r.IsGame).ToList(),
            _       => _vm.Rules.ToList()
        };
        RuleList.ItemsSource = view;
    }

    private void FilterAllClick(object _, RoutedEventArgs __)   { _vm.ActiveFilter = "all";   ApplyFilter(); }
    private void FilterGamesClick(object _, RoutedEventArgs __) { _vm.ActiveFilter = "games"; ApplyFilter(); }
    private void FilterAppsClick(object _, RoutedEventArgs __)  { _vm.ActiveFilter = "apps";  ApplyFilter(); }

    private async void AddAppClick(object _, RoutedEventArgs __)
    {
        var dlg = new AddPriorityRuleDialog(_games) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Result is { } rule)
        {
            await _vm.AddAsync(rule);
            ApplyFilter();
        }
    }

    private async void ApplyRecommendedClick(object _, RoutedEventArgs __)
    {
        var confirm = MessageBox.Show(
            "Apply recommended settings to every detected game?\n\n" +
            "  • Priority = High\n" +
            "  • Protect from RAM cleanups\n" +
            "  • Game Booster on launch\n\n" +
            "Games already in your list will be reset to these values. Custom apps are untouched.",
            "Apply Recommended",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        var (added, updated) = await _vm.ApplyRecommendedToAllGamesAsync();
        ApplyFilter();

        if (added == 0 && updated == 0)
        {
            MessageBox.Show("Everything was already on recommended settings.", "Apply Recommended");
            return;
        }

        var parts = new List<string>();
        if (added > 0)   parts.Add(added == 1 ? "added 1 game" : $"added {added} games");
        if (updated > 0) parts.Add(updated == 1 ? "reset 1 existing rule" : $"reset {updated} existing rules");
        MessageBox.Show("Recommended settings applied — " + string.Join(" and ", parts) + ".", "Apply Recommended");
    }

    private async void RescanClick(object _, RoutedEventArgs __)
    {
        var added = await _vm.RescanRunningAppsAsync();
        ApplyFilter();
        MessageBox.Show(
            added == 0 ? "No new running apps found." : $"Added {added} new app(s).",
            "Re-scan apps");
    }

    private async void RemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PriorityRuleVm vm) return;
        await _vm.RemoveAsync(vm);
        ApplyFilter();
    }

    private async void RuleChanged(object _, RoutedEventArgs __)
    {
        // Persist on any inline rule change. View-model handles ToList → SaveAsync → engine reload.
        await _vm.UpdateRuleAsync(null!);
    }

    // ---- Multi-select mode --------------------------------------------------

    private void MultiSelectToggleClick(object sender, RoutedEventArgs e)
    {
        _vm.MultiSelectMode = MultiSelectToggle.IsChecked == true;
        if (!_vm.MultiSelectMode)
        {
            foreach (var r in _vm.Rules) r.IsSelected = false;
        }
        UpdateSelectionCount();
    }

    private void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_vm.MultiSelectMode) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not PriorityRuleVm vm) return;

        var newValue = !vm.IsSelected;
        vm.IsSelected = newValue;
        _dragSelectValue = newValue;
        UpdateSelectionCount();
        Mouse.Capture(RuleList);
        e.Handled = true;
    }

    private void Row_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_vm.MultiSelectMode) return;
        if (_dragSelectValue is not bool target) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not PriorityRuleVm vm) return;

        if (vm.IsSelected == target) return;
        vm.IsSelected = target;
        UpdateSelectionCount();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (Mouse.Captured == RuleList) RuleList.ReleaseMouseCapture();
        _dragSelectValue = null;
    }

    private void UpdateSelectionCount()
    {
        var n = _vm.Rules.Count(r => r.IsSelected);
        SelectionCountText.Text = n == 1 ? "1 selected" : $"{n} selected";
    }

    private IReadOnlyList<PriorityRuleVm> Selected =>
        _vm.Rules.Where(r => r.IsSelected).ToList();

    private async void BulkPriorityClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string priorityName) return;
        if (!Enum.TryParse<PriorityLevel>(priorityName, out var level)) return;
        var picks = Selected;
        if (picks.Count == 0) return;
        foreach (var r in picks) r.Priority = level;
        await _vm.UpdateRuleAsync(null!);
    }

    private async void BulkProtectClick(object _, RoutedEventArgs __)
    {
        var picks = Selected;
        if (picks.Count == 0) return;
        // Toggle: if every selected row already has Protect on, turn them all off. Otherwise turn them all on.
        var allOn = picks.All(r => r.ProtectFromRamCleanup);
        var target = !allOn;
        foreach (var r in picks) r.ProtectFromRamCleanup = target;
        await _vm.UpdateRuleAsync(null!);
    }

    private async void BulkBoosterClick(object _, RoutedEventArgs __)
    {
        var picks = Selected;
        if (picks.Count == 0) return;
        var allOn = picks.All(r => r.GameBooster);
        var target = !allOn;
        foreach (var r in picks) r.GameBooster = target;
        await _vm.UpdateRuleAsync(null!);
    }

    private void DiscardSelectionClick(object _, RoutedEventArgs __)
    {
        foreach (var r in _vm.Rules) r.IsSelected = false;
        _vm.MultiSelectMode = false;
        MultiSelectToggle.IsChecked = false;
        UpdateSelectionCount();
    }
}
