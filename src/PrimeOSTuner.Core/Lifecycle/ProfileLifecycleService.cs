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
        FrameRecordingService? recorder = null,
        Func<bool>? suspendForEveryGame = null)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
        _suspender = suspender;
        _sentinel = sentinel;
        _recorder = recorder;
        _suspendForEveryGame = suspendForEveryGame;
    }

    // When true, background apps are frozen for EVERY detected game, no profile needed —
    // "pause the RGB and sync apps while I play, wake them after" as a standing rule
    // rather than something earned by assigning a boost profile per game.
    private readonly Func<bool>? _suspendForEveryGame;

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
        try { await HandleGameStartedAsync(game); }
        catch { /* a game launch must never surface a background error */ }
    }

    /// <summary>Awaitable core of the GameStarted handler — the event wrapper is fire-and-forget,
    /// but this lets callers (and tests) drive it deterministically.</summary>
    public async Task HandleGameStartedAsync(KnownGame game, CancellationToken ct = default)
    {
        try
        {
            var pid = ResolvePid(game);
            lock (_stateGate) { _currentGame = game; _currentPid = pid; }

            // Sentinel watching + frame/FPS recording run for ANY detected game — they don't
            // require a profile. (Previously these were gated behind a profile assignment,
            // which is why Sentinel "did nothing" and the FPS overlay stayed blank for games
            // the user hadn't assigned a profile to.)
            try { _sentinel?.OnGameStarted(game, pid); }
            catch { /* Sentinel must never break a game launch */ }

            try { _recorder?.OnGameStarted(game, pid); }
            catch { /* recording must never break a game launch */ }

            // Suspension by standing rule: no profile required when the user has switched
            // it on globally. SuspendBackgroundApps is pid-idempotent, so hitting it again
            // below (profile path) double-freezes nothing; ResumeAll on stop wakes both.
            if (_suspendForEveryGame?.Invoke() == true)
            {
                try { _suspender?.SuspendBackgroundApps(); }
                catch { /* freezing optional apps must never break a game launch */ }
            }

            // The optimization profile (and profile-driven suspension) only apply if the
            // user assigned one to this game.
            var modeName = await _profiles.GetProfileForAsync(game.Id);
            if (modeName is null) return;
            if (!_profileLookup.TryGetValue(modeName, out var profile)) return;

            var result = await _applier.ApplyAsync(profile);
            await _active.SaveAsync(new ActiveTweaksRecord(
                game.Id, profile.Id, DateTime.UtcNow, result.Outcomes));

            try { _suspender?.SuspendBackgroundApps(); }
            catch { /* freezing optional apps must never break a game launch */ }
        }
        catch
        {
        }
    }

    private async void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        try { await HandleGameStoppedAsync(e); }
        catch { /* never surface a background error on game exit */ }
    }

    /// <summary>Awaitable core of the GameStopped handler.</summary>
    public async Task HandleGameStoppedAsync(GameStoppedArgs e, CancellationToken ct = default)
    {
        try
        {
            lock (_stateGate) { _currentGame = null; _currentPid = 0; }

            try { _sentinel?.OnGameStopped(); }
            catch { /* never trap on Sentinel teardown */ }

            try { if (_recorder is not null) await _recorder.OnGameStoppedAsync(e.Game); }
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

    /// <summary>
    /// The process that is actually the game. "First process with this name" was wrong
    /// whenever more than one exists — and for Minecraft that is the normal case: every
    /// Java program is javaw.exe/java.exe (Gradle daemons, mod dev clients, launcher
    /// helpers). Frame recording then attached to a background Java process that barely
    /// draws, and saved sessions of 16 frames over minutes with nonsense lows and highs.
    /// The game is the candidate that owns a window; among those, the biggest.
    /// </summary>
    public static int ResolvePid(KnownGame game)
    {
        var candidates = new List<(int Pid, bool HasWindow, long Memory)>();
        foreach (var exeName in game.ExecutableNames)
        {
            if (string.IsNullOrEmpty(exeName)) continue;
            var trimmed = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? exeName[..^4]
                : exeName;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(trimmed))
            {
                try { candidates.Add((p.Id, p.MainWindowHandle != IntPtr.Zero, p.WorkingSet64)); }
                catch { /* exited mid-scan */ }
                finally { p.Dispose(); }
            }
        }
        return PickGamePid(candidates);
    }

    public static int PickGamePid(IEnumerable<(int Pid, bool HasWindow, long Memory)> candidates) =>
        candidates
            .OrderByDescending(c => c.HasWindow)
            .ThenByDescending(c => c.Memory)
            .Select(c => c.Pid)
            .FirstOrDefault();
}
