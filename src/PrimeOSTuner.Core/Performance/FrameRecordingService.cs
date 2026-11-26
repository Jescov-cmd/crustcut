using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Streams live per-frame times from PresentMon while a game runs. Drives two things:
///   • a live FPS counter (<see cref="CurrentFps"/> / <see cref="FpsChanged"/>) for the overlay
///     and the Sentinel tab, computed over a rolling ~1-second window;
///   • the post-game session stats (avg / 1% low / highest FPS), computed from all the
///     accumulated frame times when the game stops and persisted via the session store.
/// Every entry point is failure-isolated — recording must never break a game launch.
/// </summary>
public sealed class FrameRecordingService
{
    private readonly IPresentMonRunner _runner;
    private readonly IFrameSessionStore _store;
    private readonly object _gate = new();

    private DateTime _startedAt;
    private KnownGame? _game;
    private CancellationTokenSource? _cts;
    private List<double>? _frameTimes;          // all frames this session (for final stats)
    private readonly Queue<double> _recent = new();
    private double _recentSumMs;
    private DateTime _lastFpsRaiseUtc;
    private DateTime _lastFrameUtc;
    private StutterTimeline? _timeline;
    private IReadOnlyList<MemoryHog> _hogs = Array.Empty<MemoryHog>();
    private double _hogsAtRam;
    private DateTime _hogsTakenUtc;
    private int _capturingHogs;

    private readonly Func<KnownGame, int>? _resolvePid;
    private readonly Func<KnownGame, long?>? _gameHeapLimit;
    private long? _heapBytes;      // the game's own memory ceiling, read while it runs
    private long _totalRamBytes;
    private readonly Action<string>? _log;

    /// <param name="resolvePid">Finds the game's process again if capture drops out
    /// mid-game (the game restarted a process, or the recorder died).</param>
    /// <param name="log">Where "attached / saved / discarded, and why" goes. Recording
    /// used to fail silently — a whole Roblox session vanished with no trace of why.</param>
    public FrameRecordingService(IPresentMonRunner runner, IFrameSessionStore store, string framesDir,
        Func<KnownGame, int>? resolvePid = null, Action<string>? log = null,
        Func<KnownGame, long?>? gameHeapLimit = null)
    {
        _runner = runner;
        _store = store;
        _resolvePid = resolvePid;
        _gameHeapLimit = gameHeapLimit;
        _log = log;
        // framesDir kept for backwards-compatible DI; streaming no longer writes a CSV.
    }

    /// <summary>Live frames-per-second over the last ~1 second, or 0 when not in a game.</summary>
    public double CurrentFps { get; private set; }

    /// <summary>Raised (throttled to a few times/sec) when the live FPS updates.</summary>
    public event EventHandler? FpsChanged;

    // One game is recorded at a time: the frame recorder can't run twice (a second
    // PresentMon stops the first one's capture session). Starts and stops are applied in
    // the order they happen, through this one queue, so two games overlapping can never
    // mix their frames into one session or lose a session without a trace.
    private readonly SemaphoreSlim _sequence = new(1, 1);

    /// <summary>A game started. If another game is being recorded, that session is saved
    /// first and recording moves to the new one — the game just launched is the one
    /// being played.</summary>
    public void OnGameStarted(KnownGame game, int pid) => _ = StartAsync(game, pid);

    private async Task StartAsync(KnownGame game, int pid)
    {
        await _sequence.WaitAsync();
        try
        {
            KnownGame? current;
            lock (_gate) current = _game;
            if (current?.Id == game.Id) return;   // already recording this game
            if (current is not null)
            {
                _log?.Invoke($"recording: {game.DisplayName} started while {current.DisplayName} was running — " +
                             $"saving {current.DisplayName} and recording {game.DisplayName} from here");
                await StopCoreAsync();
            }
            BeginSession(game, pid);
        }
        catch (Exception ex) { _log?.Invoke($"recording: {game.DisplayName} — couldn't start: {ex.Message}"); }
        finally { _sequence.Release(); }
    }

    private void BeginSession(KnownGame game, int pid)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _startedAt = DateTime.UtcNow;
            _game = game;
            _frameTimes = new List<double>();
            _timeline = new StutterTimeline(_startedAt);
            _heapBytes = null;
            _hogs = Array.Empty<MemoryHog>();
            _hogsAtRam = 0;
            _hogsTakenUtc = DateTime.MinValue;
            _recent.Clear();
            _recentSumMs = 0;
            CurrentFps = 0;
            _cts = cts;
        }

        // Fire-and-forget; OnFrame accumulates + updates the live FPS.
        _ = CaptureLoopAsync(game, pid, cts.Token);
    }

    private int FrameCount { get { lock (_gate) return _frameTimes?.Count ?? 0; } }

    /// <summary>
    /// Keeps frame capture attached for the whole game. The recorder ends by itself when
    /// the process it watches exits — which is normal at the end of a game (then the stop
    /// event cancels this loop within a moment), but mid-game it means the game moved to
    /// a new process or the recorder died. Either way: find the game again and reattach,
    /// instead of recording nothing for the rest of the session.
    /// </summary>
    private async Task CaptureLoopAsync(KnownGame game, int pid, CancellationToken ct)
    {
        const int MaxReattaches = 10;
        for (var attempt = 0; !ct.IsCancellationRequested; attempt++)
        {
            if (pid == 0)
            {
                if (attempt == 0) _log?.Invoke($"recording: {game.DisplayName} — can't find the game's process yet");
            }
            else
            {
                _log?.Invoke(attempt == 0
                    ? $"recording: {game.DisplayName} — capturing frames (pid {pid})"
                    : $"recording: {game.DisplayName} — capture dropped after {FrameCount:N0} frames, reattached (pid {pid})");
                using var attach = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _ = SwitchIfSilentAsync(game, pid, attach);
                try { await _runner.StreamAsync(pid, OnFrame, attach.Token); }
                catch { /* the runner never throws on purpose; treat like an early end */ }
                attach.Cancel();   // stops the silence watch
            }

            if (ct.IsCancellationRequested) return;
            if (attempt >= MaxReattaches)
            {
                _log?.Invoke($"recording: {game.DisplayName} — giving up after {MaxReattaches} reattaches");
                return;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), ct); }
            catch (OperationCanceledException) { return; }   // the game ended: normal
            pid = _resolvePid?.Invoke(game) ?? pid;
        }
    }

    /// <summary>Attached to a process that never draws — Minecraft's launcher is a Java
    /// process too, and on 2026-09-30 a whole evening of sessions recorded 0 frames from
    /// it. No frames for <see cref="SilenceLimit"/> while the game resolves to a different
    /// process: let go, and the capture loop attaches to that one.</summary>
    private async Task SwitchIfSilentAsync(KnownGame game, int pid, CancellationTokenSource attach)
    {
        var attachedUtc = DateTime.UtcNow;
        try
        {
            while (!attach.IsCancellationRequested)
            {
                await Task.Delay(SilenceLimit, attach.Token);
                DateTime last;
                lock (_gate) last = _lastFrameUtc;
                if (last >= attachedUtc) return;   // frames are flowing: nothing to fix
                var now = _resolvePid?.Invoke(game) ?? 0;
                if (now == 0 || now == pid) continue;
                _log?.Invoke($"recording: {game.DisplayName} — no frames from pid {pid} in {SilenceLimit.TotalSeconds:F0} s, switching to pid {now}");
                attach.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>How long a capture may stay silent before checking it's on the right process.</summary>
    public TimeSpan SilenceLimit { get; init; } = TimeSpan.FromSeconds(20);

    private void OnFrame(double ms)
    {
        bool raise = false;
        lock (_gate)
        {
            _frameTimes?.Add(ms);
            _timeline?.AddFrame(DateTime.UtcNow, ms);
            _lastFrameUtc = DateTime.UtcNow;

            _recent.Enqueue(ms);
            _recentSumMs += ms;
            while (_recentSumMs > 1000 && _recent.Count > 1)
                _recentSumMs -= _recent.Dequeue();

            CurrentFps = _recentSumMs > 0 ? _recent.Count / (_recentSumMs / 1000.0) : 0;

            // Throttle UI notifications to ~5/sec regardless of frame rate.
            var now = DateTime.UtcNow;
            if ((now - _lastFpsRaiseUtc).TotalMilliseconds >= 200)
            {
                _lastFpsRaiseUtc = now;
                raise = true;
            }
        }
        if (raise) FpsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// One system reading (once a second, from the shared sampler). Ignored unless a game
    /// is being recorded. When the game is hitching while RAM is tight, notes which apps
    /// hold the most memory — at most once a minute, keeping the fullest-RAM moment — so
    /// the verdict can name names instead of just saying "memory".
    /// </summary>
    public void OnSystemSample(Monitoring.SystemSample s)
    {
        KnownGame? game;
        bool capture;
        lock (_gate)
        {
            if (_timeline is null) return;
            _timeline.AddSystem(s.TakenAtUtc, s.CpuPercent, s.GpuPercent, s.RamPercent, s.PageInputPerSec,
                s.MaxCoreDpcPercent);
            game = _game;
            _totalRamBytes = s.RamTotalBytes;
            if (game is not null && _gameHeapLimit?.Invoke(game) is long heap) _heapBytes = heap;
            capture = s.RamPercent >= 85
                      && s.RamPercent > _hogsAtRam
                      && (s.TakenAtUtc - _hogsTakenUtc).TotalSeconds >= 60
                      && _timeline.StutteredRecently(s.TakenAtUtc);
            if (capture) { _hogsTakenUtc = s.TakenAtUtc; }
        }
        if (!capture || game is null || Interlocked.Exchange(ref _capturingHogs, 1) == 1) return;

        _ = Task.Run(() =>
        {
            try
            {
                var hogs = TopMemoryUsers(game.ExecutableNames);
                lock (_gate)
                {
                    if (_game != game) return;   // session ended meanwhile
                    _hogs = hogs;
                    _hogsAtRam = s.RamPercent;
                }
            }
            catch { /* diagnosis is a nicety — never disturb the game */ }
            finally { Volatile.Write(ref _capturingHogs, 0); }
        });
    }

    /// <summary>The five apps holding the most memory, grouped by name, excluding the
    /// game itself (it's supposed to be using memory) and Windows' own bookkeeping.</summary>
    private static IReadOnlyList<MemoryHog> TopMemoryUsers(IEnumerable<string> gameExes)
    {
        var skip = new HashSet<string>(
            gameExes.Select(e => e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? e[..^4] : e),
            StringComparer.OrdinalIgnoreCase) { "Memory Compression", "System", "Registry", "Idle" };
        var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (!skip.Contains(p.ProcessName))
                    totals[p.ProcessName] = totals.GetValueOrDefault(p.ProcessName) + p.WorkingSet64;
            }
            catch { /* exited mid-scan */ }
            finally { p.Dispose(); }
        }
        return totals.OrderByDescending(kv => kv.Value).Take(5)
            .Select(kv => new MemoryHog(kv.Key, kv.Value)).ToList();
    }

    /// <summary>A game stopped. Ends the session only if it's the game being recorded —
    /// a second game closing must not end (or discard) the one still being played.</summary>
    public async Task OnGameStoppedAsync(KnownGame? stopped = null)
    {
        await _sequence.WaitAsync();
        try
        {
            KnownGame? current;
            lock (_gate) current = _game;
            if (stopped is not null && current is not null && current.Id != stopped.Id) return;
            await StopCoreAsync();
        }
        finally { _sequence.Release(); }
    }

    private async Task StopCoreAsync()
    {
        KnownGame? game;
        List<double>? frames;
        DateTime startedAt;
        CancellationTokenSource? cts;
        StutterTimeline? timeline;
        IReadOnlyList<MemoryHog> hogs;
        long? heapBytes;
        long totalRam;
        lock (_gate)
        {
            game = _game;
            frames = _frameTimes;
            startedAt = _startedAt;
            cts = _cts;
            timeline = _timeline;
            hogs = _hogs;
            heapBytes = _heapBytes;
            totalRam = _totalRamBytes;
            _game = null;
            _frameTimes = null;
            _timeline = null;
            _cts = null;
            _recent.Clear();
            _recentSumMs = 0;
            CurrentFps = 0;
        }

        try { cts?.Cancel(); } catch { }
        try { await _runner.StopAsync(); } catch { /* best effort */ }
        FpsChanged?.Invoke(this, EventArgs.Empty);   // clear the live counter

        if (game is null || frames is null) return;

        try
        {
            var stats = FrameTimeStatsCalculator.Compute(frames);
            var duration = DateTime.UtcNow - startedAt;
            if (stats.SampleCount < 10)
            {
                // Say so. A session that silently doesn't appear reads as "Crustcut isn't
                // measuring my games", with nothing to go on.
                _log?.Invoke($"recording: {game.DisplayName} — not saved: only {stats.SampleCount} frame(s) captured " +
                             $"in {duration.TotalMinutes:F0} min (the frame recorder couldn't attach to the game)");
                return;
            }

            var session = new FrameSession(
                GameId: game.Id,
                GameName: game.DisplayName,
                StartedAt: startedAt,
                Duration: duration,
                Stats: stats,
                Stutter: timeline?.Analyze(frames, hogs, heapBytes, totalRam));
            _store.Save(session);
            _log?.Invoke($"recording: {game.DisplayName} — saved {stats.SampleCount:N0} frames, " +
                         $"{stats.AvgFps:F0} fps average, 1% low {stats.OnePctLowFps:F0}");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"recording: {game.DisplayName} — session could not be saved: {ex.Message}");
        }
        finally { cts?.Dispose(); }
    }
}
