using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed record GameStoppedArgs(KnownGame Game, string Reason);

public interface IGameProcessWatcher
{
    event EventHandler<KnownGame>? GameStarted;
    event EventHandler<GameStoppedArgs>? GameStopped;

    void Start();
    void Stop();
    bool IsRunning { get; }
    Task TickAsync(CancellationToken ct = default);
}
