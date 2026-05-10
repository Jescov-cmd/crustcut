namespace PrimeOSTuner.Core.Memory;

public interface IGameBooster
{
    Task QueueAsync(int launchingPid, IEnumerable<int> protectedPids, CancellationToken ct = default);
}
