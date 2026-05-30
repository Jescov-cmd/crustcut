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

    public FrameRecordingService(IPresentMonRunner runner, IFrameSessionStore store, string framesDir)
    {
        _runner = runner;
        _store = store;
        // framesDir kept for backwards-compatible DI; streaming no longer writes a CSV.
    }

    /// <summary>Live frames-per-second over the last ~1 second, or 0 when not in a game.</summary>
    public double CurrentFps { get; private set; }

    /// <summary>Raised (throttled to a few times/sec) when the live FPS updates.</summary>
    public event EventHandler? FpsChanged;

    public void OnGameStarted(KnownGame game, int pid)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _startedAt = DateTime.UtcNow;
            _game = game;
            _frameTimes = new List<double>();
            _recent.Clear();
            _recentSumMs = 0;
            CurrentFps = 0;
            _cts = cts;
        }

        // Fire-and-forget stream; OnFrame accumulates + updates the live FPS.
        _ = _runner.StreamAsync(pid, OnFrame, cts.Token);
    }

    private void OnFrame(double ms)
    {
        bool raise = false;
        lock (_gate)
        {
            _frameTimes?.Add(ms);

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

    public async Task OnGameStoppedAsync()
    {
        KnownGame? game;
        List<double>? frames;
        DateTime startedAt;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            game = _game;
            frames = _frameTimes;
            startedAt = _startedAt;
            cts = _cts;
            _game = null;
            _frameTimes = null;
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
            if (stats.SampleCount < 10) return;   // too short to be useful

            _store.Save(new FrameSession(
                GameId: game.Id,
                GameName: game.DisplayName,
                StartedAt: startedAt,
                Duration: DateTime.UtcNow - startedAt,
                Stats: stats));
        }
        catch { /* never break the game-stopped path */ }
        finally { cts?.Dispose(); }
    }
}
