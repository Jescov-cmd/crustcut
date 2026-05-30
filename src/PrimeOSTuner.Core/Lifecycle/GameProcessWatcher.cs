using System.Diagnostics;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed class GameProcessWatcher : IGameProcessWatcher, IDisposable
{
    private readonly Func<Task<IReadOnlyList<KnownGame>>> _knownGamesProvider;
    private readonly Func<string[]> _processSnapshotProvider;
    private readonly int _pollIntervalMs;
    private readonly System.Timers.Timer _timer;
    private readonly Dictionary<string, KnownGame> _running = new();
    private int _ticking;   // 0 = idle, 1 = a tick is in progress (re-entrancy guard)

    public event EventHandler<KnownGame>? GameStarted;
    public event EventHandler<GameStoppedArgs>? GameStopped;

    public bool IsRunning { get; private set; }

    public GameProcessWatcher(
        Func<Task<IReadOnlyList<KnownGame>>> knownGamesProvider,
        Func<string[]>? processSnapshotProvider = null,
        int pollIntervalMs = 2000)
    {
        _knownGamesProvider = knownGamesProvider;
        _processSnapshotProvider = processSnapshotProvider ?? DefaultSnapshot;
        _pollIntervalMs = pollIntervalMs;
        _timer = new System.Timers.Timer(_pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += async (_, _) =>
        {
            // Skip this tick if the previous one is still running. An AutoReset timer fires
            // on a thread-pool thread without waiting, so a slow tick (large Steam library /
            // slow disk) could otherwise overlap and corrupt the _running dictionary.
            if (System.Threading.Interlocked.CompareExchange(ref _ticking, 1, 0) != 0) return;
            try { await TickAsync(); }
            catch { }
            finally { System.Threading.Interlocked.Exchange(ref _ticking, 0); }
        };
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _timer.Start();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
    }

    public async Task TickAsync(CancellationToken ct = default)
    {
        var known = await _knownGamesProvider();
        var processNames = new HashSet<string>(_processSnapshotProvider(), StringComparer.OrdinalIgnoreCase);

        foreach (var game in known)
        {
            ct.ThrowIfCancellationRequested();
            var hasExe = game.ExecutableNames.Any(e => processNames.Contains(e));
            var alreadyTracked = _running.ContainsKey(game.Id);

            if (hasExe && !alreadyTracked)
            {
                _running[game.Id] = game;
                GameStarted?.Invoke(this, game);
            }
            else if (!hasExe && alreadyTracked)
            {
                _running.Remove(game.Id);
                GameStopped?.Invoke(this, new GameStoppedArgs(game, "process exit"));
            }
        }
    }

    private static string[] DefaultSnapshot()
    {
        try
        {
            return Process.GetProcesses()
                .Select(p => { try { return p.ProcessName + ".exe"; } catch { return ""; } finally { p.Dispose(); } })
                .Where(s => s.Length > 4)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
