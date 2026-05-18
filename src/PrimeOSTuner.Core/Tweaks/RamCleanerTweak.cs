using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly IProcessClient _processes;
    private readonly IRamCleanerProtectList _protectList;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Force-clear standby cache";
    public string Description => "Useful for memory-leaking games (some Unreal Engine titles). Modern Windows manages memory well — for most apps this is neutral or mildly counterproductive.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public RamCleanerTweak(IProcessClient processes, IRamCleanerProtectList protectList)
    {
        _processes = processes;
        _protectList = protectList;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var protectList = _protectList.Get();
        var attempted = protectList.Count == 0
            ? _processes.TrimAllUserProcesses()
            : _processes.TrimUserProcessesExcept(protectList);
        return Task.FromResult(TweakResult.Success($"{{\"attempted\":{attempted}}}"));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows will repopulate working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will request Windows to trim working sets of all accessible processes.");
}
