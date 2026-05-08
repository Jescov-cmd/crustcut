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
        _timer.Elapsed += async (_, _) => { try { await TickAsync(); } catch { } };
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
