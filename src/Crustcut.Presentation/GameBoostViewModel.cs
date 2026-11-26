using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Suspension;
using PrimeOSTuner.Win.Suspension;

namespace Crustcut.Presentation;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProfileApplier _applier;
    private readonly AppSettingsStore _settings;

    /// <summary>Freeze curated background apps for every detected game. Two-way bound.</summary>
    public bool SuspendInGame
    {
        get { try { return _settings.Load().SuspendBackgroundAppsInGame; } catch { return false; } }
        set
        {
            try
            {
                _settings.Update(s => s.SuspendBackgroundAppsInGame = value);
            }
            catch { }
            OnPropertyChanged(nameof(SuspendInGame));
        }
    }
    private readonly IBackgroundSuspenderService? _suspender;
    private bool _suppressSave;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isWorking;

    // Persisted toggle state — survives navigation because the VM is a singleton.
    [ObservableProperty] private bool _isBasicActive;
    [ObservableProperty] private bool _isPerformanceActive;
    [ObservableProperty] private bool _isAggressiveActive;

    public ObservableCollection<SuspendedProcessInfo> SuspendedApps { get; } = new();

    // ── Game-only apps ────────────────────────────────────────────────────────────────
    private readonly IGameOnlyApps? _gameOnly;
    private readonly IUiDispatcher? _ui;

    public bool GameOnlyAvailable => _gameOnly is not null;

    public bool GameOnlyEnabled
    {
        get => _gameOnly?.Enabled ?? false;
        set { if (_gameOnly is not null) _gameOnly.Enabled = value; OnPropertyChanged(); }
    }

    public ObservableCollection<GameOnlyRowVm> GameOnlyApps { get; } = new();
    public ObservableCollection<string> GameOnlyCandidates { get; } = new();

    [ObservableProperty] private string? _selectedCandidate;
    [ObservableProperty] private bool _showAddPicker;
    [ObservableProperty] private bool _gameOnlyListEmpty;

    public void RefreshGameOnly()
    {
        if (_gameOnly is null) return;
        var rows = _gameOnly.Status().Select(s => new GameOnlyRowVm(s)).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= GameOnlyApps.Count) GameOnlyApps.Add(rows[i]);
            else if (GameOnlyApps[i] != rows[i]) GameOnlyApps[i] = rows[i];
        }
        while (GameOnlyApps.Count > rows.Count) GameOnlyApps.RemoveAt(GameOnlyApps.Count - 1);
        // "Nothing to put to sleep" also when everything listed is closed — a lone NOT RUNNING
        // row read as a feature that does nothing.
        GameOnlyListEmpty = rows.All(r => r.StateText == "NOT RUNNING");
        OnPropertyChanged(nameof(GameOnlyEnabled));
    }

    /// <summary>Candidate list scans every process — only on demand (page shown, app added).</summary>
    public async Task RefreshCandidatesAsync()
    {
        if (_gameOnly is null) return;
        var names = await Task.Run(_gameOnly.Candidates);
        GameOnlyCandidates.Clear();
        foreach (var n in names) GameOnlyCandidates.Add(n);
        SelectedCandidate = GameOnlyCandidates.FirstOrDefault();
    }

    public async Task AddSelectedAsync()
    {
        if (_gameOnly is null || string.IsNullOrEmpty(SelectedCandidate)) return;
        _gameOnly.Add(SelectedCandidate);
        RefreshGameOnly();
        await RefreshCandidatesAsync();
    }

    public async Task RemoveAsync(string name)
    {
        if (_gameOnly is null) return;
        await Task.Run(() => _gameOnly.Remove(name));
        RefreshGameOnly();
        await RefreshCandidatesAsync();
    }

    public async Task WakeAsync(string name)
    {
        if (_gameOnly is null) return;
        await Task.Run(() => _gameOnly.Wake(name));
        RefreshGameOnly();
    }

    public GameBoostViewModel(
        ProfileApplier applier, AppSettingsStore settings,
        IBackgroundSuspenderService? suspender = null, IUiDispatcher? ui = null,
        IGameOnlyApps? gameOnly = null)
    {
        _applier = applier;
        _settings = settings;
        _suspender = suspender;
        _gameOnly = gameOnly;
        _ui = ui;
        if (_gameOnly is not null)
            _gameOnly.Changed += (_, _) => { if (_ui is not null) _ui.Post(RefreshGameOnly); else RefreshGameOnly(); };

        // Restore the last-activated mode so the toggle reflects what's still active after
        // a restart instead of resetting to off. Suppressed so restoring doesn't re-save.
        _suppressSave = true;
        try
        {
            switch (settings.Load().GameBoostMode)
            {
                case "basic": IsBasicActive = true; break;
                case "performance": IsPerformanceActive = true; break;
                case "aggressive": IsAggressiveActive = true; break;
            }
        }
        catch { }
        _suppressSave = false;

        if (_suspender is not null)
        {
            _suspender.Changed += (_, _) =>
            {
                if (ui is not null) ui.Post(RefreshSuspended);
                else RefreshSuspended();
            };
            RefreshSuspended();
        }
    }

    partial void OnIsBasicActiveChanged(bool value) => PersistMode();
    partial void OnIsPerformanceActiveChanged(bool value) => PersistMode();
    partial void OnIsAggressiveActiveChanged(bool value) => PersistMode();

    // Persist whichever mode is active (or "" when all are off) so it survives a restart.
    private void PersistMode()
    {
        if (_suppressSave) return;
        var mode = IsAggressiveActive ? "aggressive"
                 : IsPerformanceActive ? "performance"
                 : IsBasicActive ? "basic" : "";
        try
        {
            if (_settings.Load().GameBoostMode != mode) _settings.Update(s => s.GameBoostMode = mode);
        }
        catch { /* persistence is best-effort */ }
    }

    private void RefreshSuspended()
    {
        SuspendedApps.Clear();
        if (_suspender is null) return;
        foreach (var info in _suspender.Currently) SuspendedApps.Add(info);
    }

    public async Task ApplyBasicAsync()
    {
        IsBasicActive = true; IsPerformanceActive = false; IsAggressiveActive = false;
        await ApplyAsync(BuiltInProfiles.Basic);
    }

    public async Task ApplyPerformanceAsync()
    {
        IsPerformanceActive = true; IsBasicActive = false; IsAggressiveActive = false;
        await ApplyAsync(BuiltInProfiles.Performance);
    }

    public async Task ApplyAggressiveAsync()
    {
        IsAggressiveActive = true; IsBasicActive = false; IsPerformanceActive = false;
        await ApplyAsync(BuiltInProfiles.Aggressive);
    }

    private async Task ApplyAsync(ModeProfile profile)
    {
        IsWorking = true;
        StatusMessage = $"Applying {profile.DisplayName}…";
        try
        {
            // Task.Run: profile application runs synchronous tweak bodies (powercfg,
            // registry) — off the UI thread or every mode click freezes the app.
            var result = await Task.Run(() => _applier.ApplyAsync(profile));
            StatusMessage = $"{profile.DisplayName}: {result.SuccessCount} applied, {result.FailureCount} failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }
}

/// <summary>One game-only app as the list shows it.</summary>
public sealed record GameOnlyRowVm(string Name, string StateText, string Detail, bool IsFrozen)
{
    public GameOnlyRowVm(GameOnlyAppStatus s) : this(
        s.Name,
        s.State switch
        {
            GameOnlyState.Frozen => "ASLEEP",
            GameOnlyState.Awake => "AWAKE",
            _ => "NOT RUNNING",
        },
        s.State switch
        {
            GameOnlyState.Frozen when s.MemoryBytes > 0 => $"gave back {s.MemoryBytes >> 20} MB · " +
                (s.WakesForGames ? "wakes for games" : "wakes when you open it"),
            GameOnlyState.Frozen => s.WakesForGames ? "wakes for games" : "wakes when you open it",
            GameOnlyState.Awake when s.Reason is { } why => $"using {s.MemoryBytes >> 20} MB · awake: {why}",
            GameOnlyState.Awake => $"using {s.MemoryBytes >> 20} MB",
            _ => "",
        },
        s.State == GameOnlyState.Frozen) { }
}
