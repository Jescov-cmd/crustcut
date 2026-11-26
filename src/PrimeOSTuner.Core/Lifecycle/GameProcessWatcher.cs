using System.Diagnostics;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Lifecycle;

/// <summary>
/// Knows which games are running. Three ways in, fastest first:
/// - Launch events: Windows announces every new process (WMI start trace). A launch whose
///   exe could be a game triggers a check right away — detection in well under a second
///   instead of "whenever the next poll happens".
/// - Exit events: once a game is detected, its processes are watched directly, so the
///   moment it closes the watcher knows — no waiting for a poll to notice it's gone.
/// - Polling (every 2 s): the fallback that catches anything the events miss (no admin
///   rights for WMI, a process that denies exit notifications).
/// Every start and stop is logged with how quickly it was noticed, so "did it register?"
/// has an answer in the History tab.
/// </summary>
public sealed class GameProcessWatcher : IGameProcessWatcher, IDisposable
{
    private readonly Func<Task<IReadOnlyList<KnownGame>>> _knownGamesProvider;
    private readonly Func<string[]> _processSnapshotProvider;
    private readonly int _pollIntervalMs;
    private readonly System.Timers.Timer _timer;
    private readonly Dictionary<string, KnownGame> _running = new();
    private readonly Dictionary<string, DateTime> _startedUtc = new();
    private readonly Dictionary<string, List<Process>> _exitWatches = new();
    private readonly Dictionary<string, int> _missingTicks = new();
    private readonly int _exitConfirmTicks;
    private readonly IProcessWatcher? _processStarts;
    private readonly Action<string>? _log;
    private volatile HashSet<string> _interestingExes = new(StringComparer.OrdinalIgnoreCase) { "javaw.exe", "java.exe" };
    private int _ticking;   // 0 = idle, 1 = a tick is in progress (re-entrancy guard)
    private int _again;     // a tick was requested while one was running: run once more

    public event EventHandler<KnownGame>? GameStarted;
    public event EventHandler<GameStoppedArgs>? GameStopped;

    public bool IsRunning { get; private set; }

    /// <summary>Every exe name that belongs to a known game (plus Java, for Minecraft).</summary>
    public IReadOnlySet<string> KnownExecutables => _interestingExes;

    private readonly Func<IReadOnlySet<string>, KnownGame?>? _syntheticDetector;

    public GameProcessWatcher(
        Func<Task<IReadOnlyList<KnownGame>>> knownGamesProvider,
        Func<string[]>? processSnapshotProvider = null,
        int pollIntervalMs = 2000,
        Func<IReadOnlySet<string>, KnownGame?>? syntheticDetector = null,
        IProcessWatcher? processStarts = null,
        Action<string>? log = null,
        int exitConfirmTicks = 1)
    {
        _exitConfirmTicks = Math.Max(1, exitConfirmTicks);
        _knownGamesProvider = knownGamesProvider;
        _processSnapshotProvider = processSnapshotProvider ?? DefaultSnapshot;
        // Some games can't be matched by exe name — modded Minecraft is a plain
        // javaw.exe. A synthetic detector inspects deeper (command lines) and reports
        // a running game directly; it gets the same started/stopped treatment.
        _syntheticDetector = syntheticDetector;
        _processStarts = processStarts;
        _log = log;
        _pollIntervalMs = pollIntervalMs;
        _timer = new System.Timers.Timer(_pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await RunGuardedTickAsync();
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        if (_processStarts is not null) _processStarts.ProcessStarted += OnProcessStarted;
        _timer.Start();
        RequestTick();   // don't make a game that's already running wait for the first poll
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
        if (_processStarts is not null) _processStarts.ProcessStarted -= OnProcessStarted;
    }

    /// <summary>Check now rather than at the next poll. Safe to call from any thread and
    /// as often as you like: requests that arrive during a check fold into one re-check.</summary>
    public void RequestTick() => _ = Task.Run(RunGuardedTickAsync);

    private async Task RunGuardedTickAsync()
    {
        // Skip if a tick is already running — but remember to run again afterwards, so an
        // exit or launch that lands mid-tick is never lost. An AutoReset timer fires on a
        // pool thread without waiting, and overlapping ticks would corrupt _running.
        if (Interlocked.CompareExchange(ref _ticking, 1, 0) != 0)
        {
            Interlocked.Exchange(ref _again, 1);
            return;
        }
        try
        {
            do
            {
                Interlocked.Exchange(ref _again, 0);
                using var tracked = PrimeOSTuner.Core.Diagnostics.MemoryWatch.Track("game watch");
                try { await TickAsync(); }
                catch { /* one bad tick must never stop game detection */ }
            }
            while (Interlocked.Exchange(ref _again, 0) == 1);
        }
        finally { Interlocked.Exchange(ref _ticking, 0); }
    }

    /// <summary>A process started somewhere. Only launches that could be a game are worth a
    /// check — the set is every known game's exe plus Java (for Minecraft).</summary>
    private void OnProcessStarted(object? sender, ProcessStartedEvent e)
    {
        var name = e.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? e.ProcessName : e.ProcessName + ".exe";
        if (!_interestingExes.Contains(name)) return;
        // A beat of grace: the process exists, but a Java game's command line and a
        // launcher's child processes take a moment to settle.
        _ = Task.Run(async () => { await Task.Delay(300); await RunGuardedTickAsync(); });
    }

    public async Task TickAsync(CancellationToken ct = default)
    {
        var known = await _knownGamesProvider();
        var processNames = new HashSet<string>(_processSnapshotProvider(), StringComparer.OrdinalIgnoreCase);

        var interesting = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "javaw.exe", "java.exe" };
        foreach (var game in known)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var exe in game.ExecutableNames) interesting.Add(exe);
            var hasExe = game.ExecutableNames.Any(e => processNames.Contains(e));
            var alreadyTracked = _running.ContainsKey(game.Id);

            if (hasExe && !alreadyTracked) Begin(game);
            else if (hasExe) _missingTicks.Remove(game.Id);
            else if (alreadyTracked) EndIfConfirmed(game);
        }
        _interestingExes = interesting;

        // The detector gets this tick's process names so it can skip its expensive
        // command-line inspection entirely when nothing it cares about is running.
        var synthetic = _syntheticDetector?.Invoke(processNames);
        if (synthetic is not null && !_running.ContainsKey(synthetic.Id))
        {
            Begin(synthetic);
        }
        else if (synthetic is not null)
        {
            _missingTicks.Remove(synthetic.Id);
        }
        else if (_syntheticDetector is not null)
        {
            // Only synthetic ids can be ended here — exe-name games are handled above.
            var ended = _running.Values.FirstOrDefault(g => g.Id.StartsWith("java.", StringComparison.Ordinal));
            if (ended is not null) EndIfConfirmed(ended);
        }
    }

    /// <summary>
    /// A game is over only when it's missing from consecutive checks. One bad look at the
    /// process list — it can come back short when the PC is starved for memory — used to
    /// end a session that was still being played and start a "new" one two seconds later:
    /// on 2026-09-30 one Minecraft session became nine, all on the same process, none of
    /// them recorded. A real exit is confirmed by a follow-up check about 1.5 s later.
    /// </summary>
    private void EndIfConfirmed(KnownGame game)
    {
        var missing = _missingTicks.GetValueOrDefault(game.Id) + 1;
        if (missing < _exitConfirmTicks)
        {
            _missingTicks[game.Id] = missing;
            _ = Task.Run(async () => { await Task.Delay(1500); await RunGuardedTickAsync(); });
            return;
        }
        End(game);
    }

    private void Begin(KnownGame game)
    {
        _missingTicks.Remove(game.Id);
        _running[game.Id] = game;
        _startedUtc[game.Id] = DateTime.UtcNow;
        var launchedUtc = WatchExits(game);
        if (launchedUtc is DateTime at)
            _log?.Invoke($"game: {game.DisplayName} started — noticed {(DateTime.UtcNow - at).TotalSeconds:F1} s after launch");
        else
            _log?.Invoke($"game: {game.DisplayName} started");
        GameStarted?.Invoke(this, game);
    }

    private void End(KnownGame game)
    {
        _missingTicks.Remove(game.Id);
        _running.Remove(game.Id);
        StopWatchingExits(game.Id);
        var played = _startedUtc.Remove(game.Id, out var since) ? DateTime.UtcNow - since : (TimeSpan?)null;
        _log?.Invoke(played is TimeSpan t
            ? $"game: {game.DisplayName} closed after {(t.TotalMinutes >= 1 ? $"{t.TotalMinutes:F0} min" : $"{t.TotalSeconds:F0} s")}"
            : $"game: {game.DisplayName} closed");
        GameStopped?.Invoke(this, new GameStoppedArgs(game, "process exit"));
    }

    /// <summary>
    /// Asks Windows to tell us when the game's processes exit, so closing a game is noticed
    /// instantly. Returns the earliest process start time (for the "noticed N s after
    /// launch" log line), or null if none could be read — anti-cheat-protected games may
    /// refuse, in which case polling still notices the exit within a couple of seconds.
    /// </summary>
    private DateTime? WatchExits(KnownGame game)
    {
        StopWatchingExits(game.Id);
        var watched = new List<Process>();
        DateTime? earliest = null;
        foreach (var exe in game.ExecutableNames)
        {
            var name = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); } catch { continue; }
            foreach (var p in procs)
            {
                try
                {
                    var started = p.StartTime.ToUniversalTime();
                    if (earliest is null || started < earliest) earliest = started;
                    p.EnableRaisingEvents = true;
                    p.Exited += (_, _) => RequestTick();
                    watched.Add(p);
                }
                catch { p.Dispose(); }
            }
        }
        if (watched.Count > 0) _exitWatches[game.Id] = watched;
        return earliest;
    }

    private void StopWatchingExits(string gameId)
    {
        if (!_exitWatches.Remove(gameId, out var procs)) return;
        foreach (var p in procs) { try { p.Dispose(); } catch { } }
    }

    /// <summary>Every running exe name. Throws rather than answer "nothing is running"
    /// when the list can't be read — the tick is then skipped, instead of every running
    /// game being declared closed.</summary>
    private static string[] DefaultSnapshot()
    {
        var names = Process.GetProcesses()
            .Select(p => { try { return p.ProcessName + ".exe"; } catch { return ""; } finally { p.Dispose(); } })
            .Where(s => s.Length > 4)
            .ToArray();
        return names.Length > 0 ? names : throw new InvalidOperationException("process list unreadable");
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
        foreach (var id in _exitWatches.Keys.ToList()) StopWatchingExits(id);
    }
}
