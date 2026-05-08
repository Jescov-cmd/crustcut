using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.Views;

public partial class OptimizeView : UserControl
{
    private readonly TweakHistory _history;
    private readonly List<ITweak> _allTweaks;
    private readonly ObservableCollection<FilterChipVm> _chips = new();
    private string _activeKey = "all";

    public OptimizeView(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        InitializeComponent();
        _allTweaks = tweaks.Where(t => !t.IsDestructive).ToList();
        _history = history;

        _chips.Add(new FilterChipVm("all", "All", true));
        _chips.Add(new FilterChipVm("fps", "FPS & Latency"));
        _chips.Add(new FilterChipVm("network", "Network"));
        _chips.Add(new FilterChipVm("system", "System Cleanup"));
        FilterChips.ItemsSource = _chips;
        Refilter();
    }

    private static string CategoryFor(string id)
    {
        if (id.StartsWith("game.nagle") || id.StartsWith("game.network")) return "network";
        if (id == "core.junk-files" || id == "core.ram-cleaner" || id == "core.visual-effects") return "system";
        return "fps";
    }

    private void ChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not string key) return;
        _activeKey = key;
        foreach (var c in _chips) c.IsActive = c.Key == key;
        Refilter();
    }

    private void Refilter()
    {
        TweakList.ItemsSource = _activeKey == "all"
            ? _allTweaks
            : _allTweaks.Where(t => CategoryFor(t.Id) == _activeKey).ToList();
    }

    private async void PreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t })
        {
            var preview = await t.PreviewAsync();
            MessageBox.Show(preview, $"Preview — {t.DisplayName}");
        }
    }

    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t } btn)
        {
            btn.IsEnabled = false;
            try
            {
                var result = await t.ApplyAsync();
                if (result.Succeeded)
                {
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), t.Id, t.DisplayName, DateTime.UtcNow, result.UndoData, false));
                    MessageBox.Show("Applied successfully.", t.DisplayName);
                }
                else
                {
                    MessageBox.Show($"Failed: {result.Error}", t.DisplayName);
                }
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}

public sealed class FilterChipVm : INotifyPropertyChanged
{
    private bool _isActive;
    public string Key { get; }
    public string Label { get; }
    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive != value) { _isActive = value; OnChanged(); } }
    }

    public FilterChipVm(string key, string label, bool isActive = false)
    {
        Key = key; Label = label; _isActive = isActive;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
