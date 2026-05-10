using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly IProcessClient _processes;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Free idle RAM";
    public string Description => "Returns unused memory to the available pool.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public RamCleanerTweak(IProcessClient processes) { _processes = processes; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var attempted = _processes.TrimAllUserProcesses();
        return Task.FromResult(TweakResult.Success($"{{\"attempted\":{attempted}}}"));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows will repopulate working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will request Windows to trim working sets of all accessible processes.");
}
