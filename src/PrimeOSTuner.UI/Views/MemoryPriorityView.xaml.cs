using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class MemoryPriorityView : UserControl
{
    private readonly MemoryPriorityViewModel _vm;
    private readonly GameRegistry _games;

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
        var games = await _games.GetAllAsync();
        var existingPaths = new HashSet<string>(
            _vm.Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);
        var pendingCount = games.Count(g =>
            !string.IsNullOrEmpty(g.InstallPath) && !existingPaths.Contains(g.InstallPath!));

        if (pendingCount == 0)
        {
            MessageBox.Show("No new games to add.", "Apply Recommended");
            return;
        }

        var dlg = new BulkApplyGamesDialog { Owner = Window.GetWindow(this) };
        dlg.Configure(pendingCount);
        dlg.ShowDialog();
        if (!dlg.Confirmed) return;

        var added = await _vm.ApplyRecommendedToAllGamesAsync();
        ApplyFilter();
        MessageBox.Show($"Added {added} game(s).", "Apply Recommended");
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
}
