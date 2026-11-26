using System.Text.Json;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Suspension;
using PrimeOSTuner.Win.Suspension;

namespace Crustcut.App.Services;

/// <summary>
/// The sleep list: game companions (Medal…) freeze whenever no game is running and wake the
/// moment one starts; anything else the user added freezes whenever they aren't using it.
/// Rules live in <see cref="GameOnlyPolicy"/>; this class supplies the facts and does the
/// work. It also wakes apps "pause while gaming" froze, when the user brings one to the
/// front mid-game. Safety, in order of how bad the failure would be:
/// - A crash must never leave an app frozen: every freeze is written to a marker file,
///   and startup thaws anything the marker lists before doing anything else.
/// - An app the user reaches for wakes: bringing one of its windows to the front, or
///   launching it again, thaws it on the spot (a frozen window would look hung).
/// - Games always win: a game start wakes everything immediately, before the clip
///   recorder needs to hook it; after the game, a grace period lets it save the last clip.
/// Frozen processes have their working sets released — the one case where trimming is
/// honest, because a frozen process cannot fault the pages straight back.
/// </summary>
public sealed class GameOnlyAppsService : IGameOnlyApps, IDisposable
{
    private readonly AppSettingsStore _settings;
    private readonly IProcessSuspender _suspender;
    private readonly IWorkingSetTrimmer _trimmer;
    private readonly GameProcessWatcher _games;
    private readonly IProcessWatcher? _launches;
    private readonly IBackgroundSuspenderService? _pausedForGames;
    private readonly Dictionary<string, string> _whyAwake = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();

    private readonly Dictionary<int, string> _frozen = new();            // pid → listed name
    private readonly Dictionary<int, string> _frozenProcessName = new(); // pid → its own exe name
    private readonly Dictionary<string, long> _freedBytes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastForeground = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ProcessSnapshot> _lastSnapshot = Array.Empty<ProcessSnapshot>();
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private DateTime _lastGameEndedUtc = DateTime.MinValue;
    private int _gamesRunning;
    private int _tick;
    private int _ticking;
    private bool _disposed;
    private bool _started;

    public event EventHandler? Changed;

    private static string MarkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "game-only-frozen.json");

    private sealed record FrozenEntry(int Pid, string Name, string ProcessName);

    public GameOnlyAppsService(AppSettingsStore settings, IProcessSuspender suspender,
        IWorkingSetTrimmer trimmer, GameProcessWatcher games, IProcessWatcher? launches,
        IBackgroundSuspenderService? pausedForGames = null)
    {
        _pausedForGames = pausedForGames;
        _settings = settings;
        _suspender = suspender;
        _trimmer = trimmer;
        _games = games;
        _launches = launches;
        _timer = new System.Threading.Timer(_ => Tick());
    }

    /// <summary>Begins acting: thaws anything a crashed run left frozen, then watches.
    /// Separate from construction so a throwaway measuring copy of Crustcut can build the
    /// UI without touching the real instance's frozen apps.</summary>
    public void Start()
    {
        lock (_gate) { if (_started || _disposed) return; _started = true; }
        RecoverFromCrash();
        _games.GameStarted += OnGameStarted;
        _games.GameStopped += OnGameStopped;
        if (_launches is not null) _launches.ProcessStarted += OnProcessStarted;
        _timer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    // ── Settings ──────────────────────────────────────────────────────────────────────
    private AppSettings LoadSettings()
    {
        try { return _settings.Load(); } catch { return new AppSettings(); }
    }

    private void SaveSettings(Action<AppSettings> change)
    {
        try { _settings.Update(change); } catch { }
    }

    public bool Enabled
    {
        get => LoadSettings().GameOnlyAppsEnabled;
        set
        {
            SaveSettings(s => s.GameOnlyAppsEnabled = value);
            if (!value) WakeAll("turned off");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !GameOnlyPolicy.CanFreeze(name)) return;
        SaveSettings(s =>
        {
            if (!s.GameOnlyApps.Contains(name, StringComparer.OrdinalIgnoreCase)) s.GameOnlyApps.Add(name);
            s.GameOnlyExcluded.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        });
        // Just added from a list the user was looking at — don't freeze it out from under them.
        lock (_gate) _lastForeground[name] = DateTime.UtcNow;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes an app off the list and wakes it. A recommended app is remembered
    /// as excluded, or automatic detection would just put it back.</summary>
    public void Remove(string name)
    {
        Wake(name);
        SaveSettings(s =>
        {
            s.GameOnlyApps.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!s.GameOnlyExcluded.Contains(name, StringComparer.OrdinalIgnoreCase)) s.GameOnlyExcluded.Add(name);
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<string> EffectiveNames(AppSettings s, IReadOnlyList<ProcessSnapshot> snapshot)
        => GameOnlyPolicy.Effective(s.GameOnlyApps, s.GameOnlyExcluded, snapshot.Select(p => p.Name));

    /// <summary>Names this feature owns — "pause while gaming" leaves them alone.</summary>
    public IReadOnlyList<string> ListedNames()
    {
        IReadOnlyList<ProcessSnapshot> snapshot;
        lock (_gate) snapshot = _lastSnapshot;
        return EffectiveNames(LoadSettings(), snapshot);
    }

    // ── Status for the UI ─────────────────────────────────────────────────────────────
    public IReadOnlyList<GameOnlyAppStatus> Status()
    {
        var s = LoadSettings();
        IReadOnlyList<ProcessSnapshot> snapshot;
        lock (_gate) snapshot = _lastSnapshot;
        if (snapshot.Count == 0)
        {
            try { snapshot = _trimmer.Snapshot(); } catch { }
        }
        var names = EffectiveNames(s, snapshot);
        lock (_gate)
        {
            return names.Select(name =>
            {
                var forGames = GameOnlyPolicy.WakesForGames(name);
                if (_frozen.Values.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return new GameOnlyAppStatus(name, GameOnlyState.Frozen, _freedBytes.GetValueOrDefault(name),
                        WakesForGames: forGames);
                var running = snapshot.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                return running.Count == 0
                    ? new GameOnlyAppStatus(name, GameOnlyState.NotRunning, 0, WakesForGames: forGames)
                    : new GameOnlyAppStatus(name, GameOnlyState.Awake, running.Sum(p => p.WorkingSetBytes),
                        _whyAwake.GetValueOrDefault(name, "checking…"), forGames);
            }).ToList();
        }
    }

    public IReadOnlyList<string> Candidates()
    {
        var s = LoadSettings();
        IReadOnlyList<ProcessSnapshot> snapshot;
        try { snapshot = _trimmer.Snapshot(); } catch { return Array.Empty<string>(); }
        var listed = new HashSet<string>(EffectiveNames(s, snapshot), StringComparer.OrdinalIgnoreCase);

        // Apps a person would recognise: they own a window (tray apps included) and hold
        // real memory. Background services and helpers are left out.
        var byName = snapshot
            .Where(p => p.HasVisibleWindow && GameOnlyPolicy.CanFreeze(p.Name) && !listed.Contains(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.Key, Bytes: snapshot.Where(p => p.Name.Equals(g.Key, StringComparison.OrdinalIgnoreCase)).Sum(p => p.WorkingSetBytes)))
            .Where(x => x.Bytes >= 20L * 1024 * 1024)
            .ToList();
        var recommended = new HashSet<string>(GameOnlyPolicy.Recommended, StringComparer.OrdinalIgnoreCase);
        return byName
            .OrderByDescending(x => recommended.Contains(x.Name))
            .ThenByDescending(x => x.Bytes)
            .Select(x => x.Name)
            .ToList();
    }

    // ── Events ────────────────────────────────────────────────────────────────────────
    private void OnGameStarted(object? sender, PrimeOSTuner.Core.Games.KnownGame game)
    {
        Interlocked.Increment(ref _gamesRunning);
        // Only game companions wake. Everything else on the list stays asleep — its memory
        // is exactly what the game can use.
        string[] companions;
        lock (_gate) companions = _frozen.Values.Where(GameOnlyPolicy.WakesForGames)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var n in companions) Wake(n, $"{game.DisplayName} started");
    }

    private void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        if (Interlocked.Decrement(ref _gamesRunning) < 0) Interlocked.Exchange(ref _gamesRunning, 0);
        lock (_gate) _lastGameEndedUtc = DateTime.UtcNow;
    }

    /// <summary>Launching a frozen app again (Start menu, shortcut) talks to the frozen
    /// instance, which can't answer. Wake it so the launch just works.</summary>
    private void OnProcessStarted(object? sender, ProcessStartedEvent e)
    {
        var name = Path.GetFileNameWithoutExtension(e.ProcessName);
        bool frozen;
        lock (_gate) frozen = _frozen.Values.Contains(name, StringComparer.OrdinalIgnoreCase);
        if (frozen) Wake(name, "launched again");
    }

    // ── The loop ──────────────────────────────────────────────────────────────────────
    private void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1) return;
        using var tracked = PrimeOSTuner.Core.Diagnostics.MemoryWatch.Track("game-only");
        try
        {
            WatchForeground();
            if (++_tick % 5 == 0) Evaluate();   // every ~10 s
        }
        catch (Exception ex) { EngineLog.Log($"game-only: tick failed: {ex.Message}"); }
        finally { Volatile.Write(ref _ticking, 0); }
    }

    /// <summary>Every 2 s: whose window is in front? A frozen app there means the user
    /// clicked it (taskbar, alt-tab) — wake it. A listed app there is "in use".</summary>
    private void WatchForeground()
    {
        var pid = _trimmer.ForegroundPid();
        if (pid == 0) return;
        string? frozenName;
        lock (_gate) frozenName = _frozen.GetValueOrDefault(pid);
        if (frozenName is not null) { Wake(frozenName, "brought to the front"); return; }

        var name = PrimeOSTuner.Core.Windows.Games.ForegroundProbe.ImageNameOf(pid);   // every 2 s: keep it to one handle
        if (name is null) return;

        // Paused for a game and now in front: the user wants it. Wake it for the rest of the game.
        if (_pausedForGames?.Currently.Any(s => s.Pid == pid) == true)
        {
            _pausedForGames.Resume(name);
            EngineLog.Log($"game pause: woke {name} (brought to the front)");
        }

        lock (_gate)
            if (EffectiveNames(LoadSettings(), _lastSnapshot).Contains(name, StringComparer.OrdinalIgnoreCase))
                _lastForeground[name] = DateTime.UtcNow;
    }

    private void Evaluate()
    {
        var s = LoadSettings();
        var snapshot = _trimmer.Snapshot();
        var changed = false;
        var reasonChanged = false;
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            _lastSnapshot = snapshot;
            if (_disposed) return;

            // Forget pids that exited while frozen.
            var alive = snapshot.Select(p => p.Pid).ToHashSet();
            foreach (var gone in _frozen.Keys.Where(p => !alive.Contains(p)).ToList())
            { _frozen.Remove(gone); _frozenProcessName.Remove(gone); changed = true; }

            var children = snapshot.Where(p => p.ParentPid != 0)
                .GroupBy(p => p.ParentPid).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var name in EffectiveNames(s, snapshot))
            {
                var group = GroupFor(name, snapshot, children);
                if (group.Count == 0) { _whyAwake.Remove(name); continue; }
                var why = GameOnlyPolicy.WhyAwake(
                    name, now, s.GameOnlyAppsEnabled, Volatile.Read(ref _gamesRunning) > 0,
                    _lastGameEndedUtc, _startedUtc,
                    hasWindowOnScreen: group.Any(p => p.HasRestoredWindow),
                    lastForegroundUtc: _lastForeground.GetValueOrDefault(name, DateTime.MinValue));
                if (why is not null)
                {
                    if (_whyAwake.GetValueOrDefault(name) != why) reasonChanged = true;   // the UI shows it
                    _whyAwake[name] = why;
                    continue;
                }
                _whyAwake.Remove(name);

                long freed = 0;
                foreach (var p in group.Where(p => !_frozen.ContainsKey(p.Pid)))
                {
                    _suspender.Suspend(p.Pid);
                    _trimmer.TrimWorkingSet(p.Pid);
                    _frozen[p.Pid] = name;
                    _frozenProcessName[p.Pid] = p.Name;
                    freed += p.WorkingSetBytes;
                    changed = true;
                }
                if (freed > 0)
                {
                    _freedBytes[name] = _freedBytes.GetValueOrDefault(name) + freed;
                    EngineLog.Log(GameOnlyPolicy.WakesForGames(name)
                        ? $"game-only: froze {name} — no game running (released {freed >> 20} MB)"
                        : $"game-only: froze {name} — not in use (released {freed >> 20} MB)");
                }
            }
            if (changed) SaveMarker();
        }
        if (changed || reasonChanged) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The listed app's processes plus everything they spawned (encoders,
    /// helpers), minus anything that must never be frozen and Crustcut itself.</summary>
    private static List<ProcessSnapshot> GroupFor(string name, IReadOnlyList<ProcessSnapshot> snapshot,
        Dictionary<int, List<ProcessSnapshot>> children)
    {
        var result = new Dictionary<int, ProcessSnapshot>();
        var queue = new Queue<ProcessSnapshot>(
            snapshot.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p.Pid == 0 || p.Pid == Environment.ProcessId || !GameOnlyPolicy.CanFreeze(p.Name)) continue;
            if (!result.TryAdd(p.Pid, p)) continue;   // also stops recycled-pid cycles
            if (children.TryGetValue(p.Pid, out var kids)) foreach (var k in kids) queue.Enqueue(k);
        }
        return result.Values.ToList();
    }

    // ── Waking ────────────────────────────────────────────────────────────────────────
    public void Wake(string name) => Wake(name, "woken by you");

    private void Wake(string name, string why)
    {
        int[] pids;
        lock (_gate)
        {
            pids = _frozen.Where(kv => kv.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).ToArray();
            foreach (var pid in pids) { _suspender.Resume(pid); _frozen.Remove(pid); _frozenProcessName.Remove(pid); }
            _freedBytes.Remove(name);
            _lastForeground[name] = DateTime.UtcNow;   // in use now: no instant refreeze
            SaveMarker();
        }
        if (pids.Length == 0) return;
        EngineLog.Log($"game-only: woke {name} ({why})");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void WakeAll(string why)
    {
        string[] names;
        lock (_gate) names = _frozen.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var n in names) Wake(n, why);
    }

    // ── Crash safety ──────────────────────────────────────────────────────────────────
    private void SaveMarker()
    {
        try
        {
            var path = MarkerPath();
            if (_frozen.Count == 0) { File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(
                _frozen.Select(kv => new FrozenEntry(kv.Key, kv.Value,
                    _frozenProcessName.GetValueOrDefault(kv.Key, kv.Value))).ToList()));
        }
        catch { /* best effort — Dispose and the startup recovery are the backstops */ }
    }

    /// <summary>A previous run died with apps frozen. Thaw every one that is still the
    /// same process (name check guards against a recycled pid).</summary>
    private void RecoverFromCrash()
    {
        try
        {
            var path = MarkerPath();
            if (!File.Exists(path)) return;
            var entries = JsonSerializer.Deserialize<List<FrozenEntry>>(File.ReadAllText(path)) ?? new();
            var resumed = 0;
            foreach (var e in entries)
            {
                try
                {
                    using var p = System.Diagnostics.Process.GetProcessById(e.Pid);
                    if (!p.ProcessName.Equals(e.ProcessName, StringComparison.OrdinalIgnoreCase)) continue;
                    _suspender.Resume(e.Pid);
                    resumed++;
                }
                catch { /* exited — nothing to thaw */ }
            }
            File.Delete(path);
            EngineLog.Log($"game-only: previous run ended with apps frozen — thawed {resumed}");
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
        if (!_started) return;
        _games.GameStarted -= OnGameStarted;
        _games.GameStopped -= OnGameStopped;
        if (_launches is not null) _launches.ProcessStarted -= OnProcessStarted;
        WakeAll("Crustcut closing");
    }
}
