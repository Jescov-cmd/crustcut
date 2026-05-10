using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly List<TweakRowVm> _allRows;
    private readonly ObservableCollection<FilterChipVm> _chips = new();
    private string _activeKey = "all";
    private readonly HashSet<string> _pendingReboot = new();

    public OptimizeView(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        InitializeComponent();
        _history = history;
        _allRows = tweaks
            .Where(t => !t.IsDestructive)
            .Where(t => !IsCleanupTweak(t.Id))    // System Cleanup lives on the Dashboard now
            .Select(t => new TweakRowVm(t))
            .ToList();

        _chips.Add(new FilterChipVm("all", "All", true));
        _chips.Add(new FilterChipVm("fps", "FPS & Latency"));
        _chips.Add(new FilterChipVm("network", "Network"));
        _chips.Add(new FilterChipVm("system", "System"));
        _chips.Add(new FilterChipVm("privacy", "Privacy"));
        _chips.Add(new FilterChipVm("power", "Power"));
        FilterChips.ItemsSource = _chips;
        Refilter();
    }

    private static bool IsCleanupTweak(string id) =>
        id == "core.ram-cleaner"
        || id == "core.dns-flush"
        || id == "core.windows-update-cache"
        || id == "core.driver-health"
        || id == "core.driver-store-cleanup"
        || id == "core.registry-cleanup-safe";

    private void ChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not string key) return;
        _activeKey = key;
        foreach (var c in _chips) c.IsActive = c.Key == key;
        Refilter();
    }

    private void Refilter()
    {
        var filtered = _activeKey == "all"
            ? _allRows
            : _allRows.Where(r => r.CategoryKey == _activeKey).ToList();

        // Standard = no reboot needed AND no risk note. Advanced = anything with either flag.
        var standard = filtered
            .Where(r => !r.HasRisk && !r.Tweak.RequiresReboot)
            .OrderBy(r => r.Tweak.DisplayName)
            .ToList();
        var advanced = filtered
            .Where(r => r.HasRisk || r.Tweak.RequiresReboot)
            .OrderBy(r => r.HasRisk ? 1 : 0)             // reboot-only first
            .ThenBy(r => r.Tweak.DisplayName)
            .ToList();

        StandardTweakList.ItemsSource = standard;
        AdvancedTweakList.ItemsSource = advanced;
        StandardHeader.Visibility = standard.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AdvancedHeader.Visibility = advanced.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not TweakRowVm row) return;

        tb.IsEnabled = false;
        try
        {
            if (row.IsApplied)
            {
                var result = await row.Tweak.ApplyAsync();
                if (result.Succeeded)
                {
                    row.UndoData = result.UndoData;
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), row.Tweak.Id, row.Tweak.DisplayName,
                        DateTime.UtcNow, result.UndoData, false));
                    if (row.Tweak.RequiresReboot) MarkPendingReboot(row.Tweak);
                }
                else
                {
                    row.IsApplied = false;
                    MessageBox.Show($"Failed: {result.Error}", row.Tweak.DisplayName);
                }
            }
            else if (row.UndoData is not null)
            {
                var revert = await row.Tweak.RevertAsync(row.UndoData);
                if (!revert.Succeeded)
                {
                    row.IsApplied = true;
                    MessageBox.Show($"Revert failed: {revert.Error}", row.Tweak.DisplayName);
                }
                else
                {
                    row.UndoData = null;
                    if (row.Tweak.RequiresReboot) MarkPendingReboot(row.Tweak);
                }
            }
        }
        catch (Exception ex) when (IsAdminRequired(ex))
        {
            row.IsApplied = !row.IsApplied;  // revert the toggle visually
            MessageBox.Show(
                "This tweak needs administrator rights, but PrimeOS Tuner isn't running as admin.\n\n" +
                "Close the app, then right-click PrimeOSTuner.UI.exe and choose 'Run as administrator' (or run it from an admin terminal).",
                row.Tweak.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            row.IsApplied = !row.IsApplied;
            MessageBox.Show(
                $"{ex.GetType().Name}: {ex.Message}",
                $"{row.Tweak.DisplayName} — error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            tb.IsEnabled = true;
        }
    }

    // Detects "you need admin" errors regardless of the specific exception type.
    // UnauthorizedAccessException is the registry path; InvalidOperationException with
    // "requires administrator" text is the powercfg path.
    private static bool IsAdminRequired(Exception ex)
        => ex is UnauthorizedAccessException
        || (ex.Message?.Contains("requires administrator", StringComparison.OrdinalIgnoreCase) ?? false)
        || (ex.Message?.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ?? false);

    private void MarkPendingReboot(ITweak tweak)
    {
        _pendingReboot.Add(tweak.DisplayName);
        var names = string.Join(", ", _pendingReboot);
        RebootBannerDetail.Text = _pendingReboot.Count == 1
            ? $"\"{names}\" needs a restart to fully take effect."
            : $"{_pendingReboot.Count} changes need a restart: {names}.";
        RebootBanner.Visibility = Visibility.Visible;
        // The banner sits at the top of the page. If the user toggled a tweak way down
        // the list they'd never see it pop up — scroll the page back to the top.
        PageScroller.ScrollToTop();
    }

    private void DismissRebootBannerClick(object sender, RoutedEventArgs e)
    {
        RebootBanner.Visibility = Visibility.Collapsed;
        _pendingReboot.Clear();
    }

    private void RestartNowClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart Windows now? Save your work first — this will reboot in 5 seconds.",
            "Restart now", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            // /r restart, /t 5 = 5-second delay, /c message shown to user
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"PrimeOS Tuner: applying changes\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not start shutdown: {ex.Message}", "Restart now");
        }
    }
}

public sealed class TweakRowVm : INotifyPropertyChanged
{
    private bool _isApplied;
    public ITweak Tweak { get; }
    public string CategoryKey { get; }
    public string Category { get; }

    public bool IsApplied
    {
        get => _isApplied;
        set { if (_isApplied != value) { _isApplied = value; OnChanged(); } }
    }

    public string? UndoData { get; set; }

    public TweakRowVm(ITweak tweak)
    {
        Tweak = tweak;
        var (key, label) = CategoryFor(tweak);
        CategoryKey = key;
        Category = label;
        RiskNote = (tweak as ICategorizedTweak)?.RiskNote;
    }

    public string? RiskNote { get; }
    public bool HasRisk => !string.IsNullOrEmpty(RiskNote);

    private static (string Key, string Label) CategoryFor(ITweak tweak)
    {
        if (tweak is ICategorizedTweak cat)
        {
            return cat.Category switch
            {
                "fps" => ("fps", "FPS & Latency"),
                "network" => ("network", "Network"),
                "system" => ("system", "System"),
                "privacy" => ("privacy", "Privacy"),
                "power" => ("power", "Power"),
                _ => ("fps", "FPS & Latency")
            };
        }

        // Fallback for legacy tweaks (no Category property).
        if (tweak.Id.StartsWith("game.nagle") || tweak.Id.StartsWith("game.network"))
            return ("network", "Network");
        return ("fps", "FPS & Latency");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
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
