using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Glue: on GameStarted, spawn PresentMon into a per-session CSV path.
/// On GameStopped, kill it, parse the CSV, persist a FrameSession, delete
/// the CSV. Every entry point is failure-isolated — recording must never
/// break a game launch.
/// </summary>
public sealed class FrameRecordingService
{
    private readonly IPresentMonRunner _runner;
    private readonly IFrameSessionStore _store;
    private readonly string _framesDir;
    private readonly object _gate = new();

    private DateTime _startedAt;
    private KnownGame? _game;
    private string? _csvPath;

    public FrameRecordingService(IPresentMonRunner runner, IFrameSessionStore store, string framesDir)
    {
        _runner = runner;
        _store = store;
        _framesDir = framesDir;
    }

    public void OnGameStarted(KnownGame game, int pid)
    {
        var now = DateTime.UtcNow;
        var safeId = string.Concat(game.Id.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
        var path = Path.Combine(_framesDir, $"{safeId}-{now:yyyyMMddTHHmmss}.csv");

        lock (_gate)
        {
            _startedAt = now;
            _game = game;
            _csvPath = path;
        }

        // Fire-and-forget — Start runs synchronously enough but we don't want any await contract here.
        _ = StartAsync(pid, path);
    }

    private async Task StartAsync(int pid, string path)
    {
        try
        {
            var actual = await _runner.StartAsync(pid, path);
            if (!string.IsNullOrEmpty(actual))
            {
                lock (_gate) { _csvPath = actual; }
            }
        }
        catch { /* swallow — recording must never break a game launch */ }
    }

    public async Task OnGameStoppedAsync()
    {
        KnownGame? game;
        string? path;
        DateTime startedAt;
        lock (_gate)
        {
            game = _game;
            path = _csvPath;
            startedAt = _startedAt;
            _game = null;
            _csvPath = null;
        }
        if (game is null || path is null) return;

        try { await _runner.StopAsync(); }
        catch { /* best effort */ }

        try
        {
            var samples = FrameTimeParser.ParseFile(path);
            var stats = FrameTimeStatsCalculator.Compute(samples);

            if (stats.SampleCount < 10) return;   // too short to be useful

            var session = new FrameSession(
                GameId: game.Id,
                GameName: game.DisplayName,
                StartedAt: startedAt,
                Duration: DateTime.UtcNow - startedAt,
                Stats: stats);

            _store.Save(session);
        }
        catch { /* never break the game-stopped path */ }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
