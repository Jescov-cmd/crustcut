using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;

namespace Crustcut.Presentation;

/// <summary>
/// Optimize page. The WPF original kept all of this in 505 lines of code-behind with inline
/// MessageBox calls, so none of it was testable. Behaviour is preserved; the dialogs go
/// through <see cref="IDialogService"/> and the state hops through <see cref="IUiDispatcher"/>.
/// </summary>
public partial class OptimizeViewModel : ObservableObject
{
    private readonly TweakHistory _history;
    private readonly SessionTweakStore _sessionStore;
    private readonly PendingUndoStore _pendingUndo;
    private readonly IDialogService _dialogs;
    private readonly List<TweakRowVm> _allRows;

    [ObservableProperty] private string _statusText = "Checking your system…";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _activeCategory = "all";

    public ObservableCollection<TweakRowVm> VisibleRows { get; } = new();
    public IReadOnlyList<TweakRowVm> AllRows => _allRows;

    /// <summary>Tweaks that need a reboot before they fully take effect.</summary>
    public ObservableCollection<string> PendingReboot { get; } = new();

    public OptimizeViewModel(
        IEnumerable<ITweak> tweaks,
        TweakHistory history,
        SessionTweakStore sessionStore,
        PendingUndoStore pendingUndo,
        IDialogService dialogs)
    {
        _history = history;
        _sessionStore = sessionStore;
        _pendingUndo = pendingUndo;
        _dialogs = dialogs;

        // Destructive tweaks are never toggle tiles — they stay opt-in elsewhere.
        _allRows = tweaks.Where(t => !t.IsDestructive).Select(t => new TweakRowVm(t)).ToList();
        Refilter();
    }

    partial void OnSearchTextChanged(string value) => Refilter();
    partial void OnActiveCategoryChanged(string value) => Refilter();

    public void Refilter()
    {
        var q = SearchText.Trim();
        IEnumerable<TweakRowVm> rows = _allRows;

        if (!string.Equals(ActiveCategory, "all", StringComparison.OrdinalIgnoreCase))
            rows = rows.Where(r => r.CategoryKey == ActiveCategory);

        if (q.Length > 0)
            rows = rows.Where(r =>
                r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(q, StringComparison.OrdinalIgnoreCase));

        VisibleRows.Clear();
        foreach (var r in rows) VisibleRows.Add(r);
    }

    /// <summary>
    /// Probes every tweak and reflects the real system state, then backfills the
    /// startup-enforce store so anything already applied is re-enforced next launch.
    /// </summary>
    public async Task InitializeAppliedStatesAsync()
    {
        try
        {
            var states = await Task.Run(() =>
                TweakStateInitializer.ComputeAsync(_allRows.Select(r => r.Tweak), _history, _pendingUndo));

            // Build the lookup without throwing on a duplicate id (last wins).
            var byId = new Dictionary<string, TweakRowVm>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _allRows) byId[r.Tweak.Id] = r;

            foreach (var s in states)
            {
                if (!byId.TryGetValue(s.TweakId, out var row)) continue;
                row.UndoData = s.UndoData;
                row.IsApplied = s.IsApplied;
            }

            var appliedIds = states.Where(s => s.IsApplied).Select(s => s.TweakId).ToList();

            // Backfill the startup-enforce store with everything already applied. Tweaks the
            // user turned on in older builds (or via profiles) were never recorded, so when
            // Windows quietly reverted one nothing restored it.
            if (appliedIds.Count > 0)
            {
                try { await _sessionStore.AddManyAsync(appliedIds); }
                catch { /* enforcement is best-effort; never block the page on it */ }
            }

            var total = _allRows.Count;
            StatusText = appliedIds.Count == 0
                ? $"No optimizations active yet — toggle any of the {total} tiles to apply one."
                : $"{appliedIds.Count} of {total} optimizations active. Toggle any tile to change it.";
        }
        catch
        {
            StatusText = "Toggle each tile on or off. The tile lights up while a tweak is active.";
        }
    }

    /// <summary>
    /// Applies or reverts <paramref name="row"/> to match <paramref name="wantApplied"/>,
    /// then re-probes so the tile reports what is actually on the system rather than the
    /// switch the user flicked.
    /// </summary>
    public async Task ToggleAsync(TweakRowVm row, bool wantApplied)
    {
        row.IsBusy = true;
        try
        {
            if (wantApplied) await ApplyAsync(row);
            else await RevertAsync(row);
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            // Crustcut runs elevated, so access-denied here is NOT "you forgot to run as
            // admin" — it's Windows refusing because the value is tamper-protected or
            // policy-managed (e.g. some Win11 taskbar settings).
            await _dialogs.ShowAsync(
                $"{row.DisplayName} — blocked by Windows",
                "Windows blocked this change. This setting is protected or managed by a policy " +
                "(for example some Windows 11 taskbar options), so it can't be changed this way — " +
                "even as administrator.",
                DialogKind.Warning);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync($"{row.DisplayName} — error",
                $"{ex.GetType().Name}: {ex.Message}", DialogKind.Error);
        }
        finally
        {
            // Re-probe so "active" means detected-active. If an apply silently didn't take,
            // or a revert failed, the toggle snaps back to the truth.
            try { row.IsApplied = await row.Tweak.ProbeAsync() == TweakState.Applied; }
            catch { /* probe failure: leave the optimistic state alone */ }
            row.IsBusy = false;
        }
    }

    private async Task ApplyAsync(TweakRowVm row)
    {
        var result = await row.Tweak.ApplyAsync();
        if (!result.Succeeded)
        {
            await _dialogs.ShowAsync(row.DisplayName, $"Failed: {result.Error}", DialogKind.Error);
            return;
        }

        row.UndoData = result.UndoData;
        // Mark reboot BEFORE writing history so a history-write failure can't swallow it.
        if (row.RequiresReboot) MarkPendingReboot(row);
        try
        {
            await _history.AppendAsync(new HistoryEntry(
                Guid.NewGuid(), row.Tweak.Id, row.DisplayName,
                DateTime.UtcNow, result.UndoData, false));
        }
        catch { /* history is advisory — never let a write failure cancel the apply */ }
        // Durable undo survives a history clear / expiry. SetIfAbsent keeps the pristine value.
        try { await _pendingUndo.SetIfAbsentAsync(row.Tweak.Id, result.UndoData); } catch { }
        // Record that the user wants this ON, so startup re-applies it if Windows reverts it.
        try { await _sessionStore.AddAsync(row.Tweak.Id); } catch { }
    }

    private async Task RevertAsync(TweakRowVm row)
    {
        TweakResult revert;
        if (row.UndoData is not null)
        {
            revert = await row.Tweak.RevertAsync(row.UndoData);
        }
        else if (row.Tweak is ISelfRevertingTweak selfRevert)
        {
            // Applied before tracking, outside the app, or history cleared — restore the
            // default so the toggle can't get stuck ON.
            revert = await selfRevert.RevertToDefaultAsync();
        }
        else
        {
            await _dialogs.ShowAsync(row.DisplayName,
                "Crustcut doesn't have the undo information for this optimizer (it was applied " +
                "before tracking, or outside the app). Turn it on again, then off, to reset it.");
            return;
        }

        if (!revert.Succeeded)
        {
            await _dialogs.ShowAsync(row.DisplayName, $"Revert failed: {revert.Error}", DialogKind.Error);
            return;
        }

        row.UndoData = null;
        try { await _pendingUndo.RemoveAsync(row.Tweak.Id); } catch { }
        if (row.RequiresReboot) MarkPendingReboot(row);
        try { await _sessionStore.RemoveAsync(row.Tweak.Id); } catch { }
    }

    private void MarkPendingReboot(TweakRowVm row)
    {
        if (!PendingReboot.Contains(row.DisplayName)) PendingReboot.Add(row.DisplayName);
    }

    // Detects "access denied" regardless of exception type. UnauthorizedAccessException is
    // the registry path; InvalidOperationException carrying "requires administrator" is the
    // powercfg path.
    private static bool IsAccessDenied(Exception ex) =>
        ex is UnauthorizedAccessException ||
        (ex is InvalidOperationException && ex.Message.Contains("administrator", StringComparison.OrdinalIgnoreCase));
}
