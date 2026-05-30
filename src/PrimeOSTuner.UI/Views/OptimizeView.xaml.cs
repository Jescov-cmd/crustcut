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
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Serilog;

namespace PrimeOSTuner.UI.Views;

public partial class OptimizeView : UserControl
{
    // One-shot cleanup actions — folded in from the old Maintenance tab. Order preserved.
    private static readonly string[] CleanupTweakIds =
    {
        "core.dns-flush",
        "core.windows-update-cache",
        "core.driver-health",
        "core.driver-store-cleanup",
        "core.shader-cache-cleanup",
    };

    private readonly TweakHistory _history;
    private readonly SessionTweakStore _sessionStore;
    private readonly List<TweakRowVm> _allRows;
    private readonly ObservableCollection<FilterChipVm> _chips = new();
    private string _activeKey = "all";
    private string _searchText = "";
    private readonly HashSet<string> _pendingReboot = new();

    public OptimizeView(IEnumerable<ITweak> tweaks, TweakHistory history, SessionTweakStore sessionStore)
    {
        InitializeComponent();
        _history = history;
        _sessionStore = sessionStore;
        var allTweaks = tweaks.ToList();

        _allRows = allTweaks
            .Where(t => !t.IsDestructive)
            .Where(t => !IsCleanupTweak(t.Id))    // cleanup actions render in the bottom section, not as toggle tiles
            .Where(t => t.Id != "core.ram-cleaner")
            .Select(t => new TweakRowVm(t))
            .ToList();

        CleanupActions.ItemsSource = allTweaks
            .Where(t => IsCleanupTweak(t.Id))
            .OrderBy(t => Array.IndexOf(CleanupTweakIds, t.Id))
            .ToList();

        _chips.Add(new FilterChipVm("all", "All", true));
        _chips.Add(new FilterChipVm("fps", "FPS & Latency"));
        _chips.Add(new FilterChipVm("network", "Network"));
        _chips.Add(new FilterChipVm("system", "System"));
        _chips.Add(new FilterChipVm("privacy", "Privacy"));
        _chips.Add(new FilterChipVm("power", "Power"));
        FilterChips.ItemsSource = _chips;
        Refilter();

        // Reflect what's ACTUALLY applied on the system. Without this, every toggle
        // showed "off" on each launch even when the tweak was applied — which read to
        // users as "it didn't save my optimizations." Runs off the UI thread because
        // some probes spawn powercfg; results are marshalled back via the dispatcher.
        _ = InitializeAppliedStatesAsync();
    }

    private async Task InitializeAppliedStatesAsync()
    {
        try
        {
            var tweaks = _allRows.Select(r => r.Tweak);
            var states = await Task.Run(() => TweakStateInitializer.ComputeAsync(tweaks, _history));

            // Defensive: build the lookup without throwing on any duplicate id (last wins).
            var byId = new Dictionary<string, TweakRowVm>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _allRows) byId[r.Tweak.Id] = r;

            int updated = 0;
            foreach (var s in states)
            {
                if (!byId.TryGetValue(s.TweakId, out var row)) continue;
                // Setting IsApplied here updates the toggle via binding but does NOT raise
                // the ToggleButton.Click handler (that only fires on real user input), so
                // this can't accidentally re-apply or revert anything.
                row.UndoData = s.UndoData;
                row.IsApplied = s.IsApplied;
                updated++;
            }

            // Diagnostic: records exactly what the tab detected, so "it shows everything off"
            // reports can be traced to probe results vs a display problem.
            var appliedIds = states.Where(s => s.IsApplied).Select(s => s.TweakId).ToList();

            // Backfill the startup-enforce store with everything already applied. Tweaks the
            // user turned on in older builds (or via Apply-All/profiles) were never recorded,
            // so when Windows quietly reverted one (classic: Game Mode after a reboot) nothing
            // restored it. Now any applied optimizer is enforced on the next launch.
            if (appliedIds.Count > 0)
            {
                try { await _sessionStore.AddManyAsync(appliedIds); }
                catch (Exception ex) { Log.Warning(ex, "Failed to backfill session-enforce store"); }
            }
            Log.Information(
                "Optimize tab loaded: {Updated} tiles updated, {Applied}/{Total} detected applied [{Ids}]",
                updated, appliedIds.Count, states.Count, string.Join(", ", appliedIds));

            // At-a-glance status so the tab never again looks like "everything reset".
            var total = _allRows.Count;
            StatusText.Text = appliedIds.Count == 0
                ? $"No optimizations active yet — toggle any of the {total} tiles to apply one."
                : $"{appliedIds.Count} of {total} optimizations active. Toggle any tile to change it.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize Optimize tile applied-states");
            StatusText.Text = "Toggle each tile on or off. The tile lights up while a tweak is active.";
        }
    }

    private static bool IsCleanupTweak(string id) => CleanupTweakIds.Contains(id);

    private async void CleanupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ITweak tweak } btn) return;

        var originalContent = btn.Content;
        btn.IsEnabled = false;
        btn.Content = "Working…";
        try
        {
            var result = await tweak.ApplyAsync();
            if (result.Succeeded)
            {
                await TryAppendHistoryAsync(tweak, result.UndoData);
                CleanupStatus.Text = $"{tweak.DisplayName} — {result.Message ?? "done."}";
            }
            else
            {
                CleanupStatus.Text = $"{tweak.DisplayName} — failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            CleanupStatus.Text = $"{tweak.DisplayName} — error: {ex.Message}";
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = originalContent;
        }
    }

    private void ChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not string key) return;
        _activeKey = key;
        foreach (var c in _chips) c.IsActive = c.Key == key;
        Refilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text?.Trim() ?? "";
        if (ClearSearchBtn is not null)
            ClearSearchBtn.Visibility = _searchText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Refilter();
    }

    private void ClearSearchClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void Refilter()
    {
        IEnumerable<TweakRowVm> filtered = _activeKey == "all"
            ? _allRows
            : _allRows.Where(r => r.CategoryKey == _activeKey);

        if (_searchText.Length > 0)
        {
            filtered = filtered.Where(r =>
                r.Tweak.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || r.Tweak.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
        var filteredList = filtered.ToList();

        // Standard = no reboot needed AND no risk note. Advanced = anything with either flag.
        var standard = filteredList
            .Where(r => !r.HasRisk && !r.Tweak.RequiresReboot)
            .OrderBy(r => r.Tweak.DisplayName)
            .ToList();
        var advanced = filteredList
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

        Log.Information("Toggle {Id}: user set to {State}", row.Tweak.Id, row.IsApplied ? "ON (apply)" : "OFF (revert)");
        tb.IsEnabled = false;
        try
        {
            if (row.IsApplied)
            {
                var result = await row.Tweak.ApplyAsync();
                Log.Information("Apply {Id}: succeeded={Ok} err={Err}", row.Tweak.Id, result.Succeeded, result.Error);
                if (result.Succeeded)
                {
                    row.UndoData = result.UndoData;
                    // Mark reboot BEFORE writing history so a history-write failure can't
                    // swallow the reboot indication.
                    if (row.Tweak.RequiresReboot) MarkPendingReboot(row.Tweak);
                    await TryAppendHistoryAsync(row.Tweak, result.UndoData);
                    // Record that the user wants this optimizer ON. On the next launch,
                    // startup re-applies it if Windows (or a driver/scheme change) has
                    // quietly reverted it — so "I turned it on" stays true.
                    await TrySessionRecordAsync(row.Tweak.Id, applied: true);
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
                    // User turned it OFF — stop enforcing it on startup.
                    await TrySessionRecordAsync(row.Tweak.Id, applied: false);
                }
            }
        }
        catch (Exception ex) when (IsAdminRequired(ex))
        {
            row.IsApplied = !row.IsApplied;  // revert the toggle visually
            MessageBox.Show(
                "This tweak needs administrator rights, but Crustcut isn't running as admin.\n\n" +
                "Close the app, then right-click Crustcut.exe and choose 'Run as administrator' (or run it from an admin terminal).",
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

    private async Task TrySessionRecordAsync(string tweakId, bool applied)
    {
        try
        {
            if (applied) await _sessionStore.AddAsync(tweakId);
            else await _sessionStore.RemoveAsync(tweakId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to record session tweak {Id} (applied={Applied})", tweakId, applied);
        }
    }

    private async Task TryAppendHistoryAsync(ITweak tweak, string? undoData)
    {
        try
        {
            await _history.AppendAsync(new HistoryEntry(
                Guid.NewGuid(), tweak.Id, tweak.DisplayName,
                DateTime.UtcNow, undoData, false));
        }
        catch (Exception ex)
        {
            // Logging is best-effort; never let a history write failure cancel the apply.
            Log.Warning(ex, "Failed to append history for tweak {Id}", tweak.Id);
        }
    }

    private void MarkPendingReboot(ITweak tweak)
    {
        Log.Information("Marking pending reboot for {Id} ({Name})", tweak.Id, tweak.DisplayName);
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
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"Crustcut: applying changes\"")
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
