using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Win.Suspension;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed class ProfileLifecycleService
{
    private readonly IGameProcessWatcher _watcher;
    private readonly GameProfileStore _profiles;
    private readonly ActiveTweaksStore _active;
    private readonly IReadOnlyDictionary<string, ModeProfile> _profileLookup;
    private readonly ProfileApplier _applier;
    private readonly IBackgroundSuspenderService? _suspender;
    private readonly ISentinelService? _sentinel;
    private readonly FrameRecordingService? _recorder;

    private readonly object _stateGate = new();
    private KnownGame? _currentGame;
    private int _currentPid;

    /// <summary>
    /// The game currently running (per the watcher's last GameStarted/Stopped event),
    /// or null if none. Lets other components — e.g. the Sentinel settings toggle —
    /// resume mid-game when they come online.
    /// </summary>
    public (KnownGame Game, int Pid)? CurrentRunningGame
    {
        get
        {
            lock (_stateGate)
            {
                if (_currentGame is null) return null;
                return (_currentGame, _currentPid);
            }
        }
    }

    public ProfileLifecycleService(
        IGameProcessWatcher watcher,
        GameProfileStore profiles,
        ActiveTweaksStore active,
        IReadOnlyDictionary<string, ModeProfile> profileLookup,
        ProfileApplier applier,
        IBackgroundSuspenderService? suspender = null,
        ISentinelService? sentinel = null,
        FrameRecordingService? recorder = null)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
        _suspender = suspender;
        _sentinel = sentinel;
        _recorder = recorder;
    }

    public void Start()
    {
        _watcher.GameStarted += OnGameStarted;
        _watcher.GameStopped += OnGameStopped;
        _watcher.Start();
    }

    public void Stop()
    {
        _watcher.GameStarted -= OnGameStarted;
        _watcher.GameStopped -= OnGameStopped;
        _watcher.Stop();
    }

    public async Task RecoverFromCrashAsync(CancellationToken ct = default)
    {
        var record = await _active.LoadAsync();
        if (record is null) return;

        await _applier.RevertAsync(record.Outcomes, ct);
        await _active.ClearAsync();
    }

    private async void OnGameStarted(object? sender, KnownGame game)
    {
        try
        {
            var modeName = await _profiles.GetProfileForAsync(game.Id);
            if (modeName is null) return;
            if (!_profileLookup.TryGetValue(modeName, out var profile)) return;

            var result = await _applier.ApplyAsync(profile);
            await _active.SaveAsync(new ActiveTweaksRecord(
                game.Id, profile.Id, DateTime.UtcNow, result.Outcomes));

            try { _suspender?.SuspendBackgroundApps(); }
            catch { /* freezing optional apps must never break a game launch */ }

            var pid = ResolvePid(game);
            lock (_stateGate) { _currentGame = game; _currentPid = pid; }

            try { _sentinel?.OnGameStarted(game, pid); }
            catch { /* Sentinel must never break a game launch */ }

            try { _recorder?.OnGameStarted(game, pid); }
            catch { /* recording must never break a game launch */ }
        }
        catch
        {
        }
    }

    private async void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        try
        {
            lock (_stateGate) { _currentGame = null; _currentPid = 0; }

            try { _sentinel?.OnGameStopped(); }
            catch { /* never trap on Sentinel teardown */ }

            try { if (_recorder is not null) await _recorder.OnGameStoppedAsync(); }
            catch { /* recording must never break game-exit teardown */ }

            try { _suspender?.ResumeAll(); }
            catch { /* never trap a stopped game in a frozen-app state */ }

            var record = await _active.LoadAsync();
            if (record is null || record.GameId != e.Game.Id) return;

            await _applier.RevertAsync(record.Outcomes);
            await _active.ClearAsync();
        }
        catch
        {
        }
    }

    private static int ResolvePid(KnownGame game)
    {
        var exeName = game.ExecutableNames.FirstOrDefault();
        if (string.IsNullOrEmpty(exeName)) return 0;

        var trimmed = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName[..^4]
            : exeName;
        var procs = System.Diagnostics.Process.GetProcessesByName(trimmed);
        try { return procs.Length > 0 ? procs[0].Id : 0; }
        finally { foreach (var p in procs) p.Dispose(); }
    }
}
