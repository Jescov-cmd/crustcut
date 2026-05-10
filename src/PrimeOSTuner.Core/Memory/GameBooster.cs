namespace PrimeOSTuner.Core.Memory;

public sealed class GameBooster : IGameBooster
{
    private readonly SafeRamCleaner _cleaner;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(2);

    public GameBooster(SafeRamCleaner cleaner)
    {
        _cleaner = cleaner;
    }

    public async Task QueueAsync(int launchingPid, IEnumerable<int> protectedPids, CancellationToken ct = default)
    {
        // Let the game finish its initial allocation phase before we start trimming.
        await Task.Delay(StartupDelay, ct);
        await _cleaner.RunAsync(launchingPid, protectedPids, ct);
    }
}
