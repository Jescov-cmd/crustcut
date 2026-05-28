using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly IProcessClient _processes;
    private readonly IRamCleanerProtectList _protectList;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Trim process working sets";
    public string Description => "Asks each user process to release memory pages it isn't actively using. Mostly helps memory-leaking games (some Unreal Engine titles); otherwise neutral or mildly counterproductive — modern Windows manages this on its own.";
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
        // Heavy synchronous I/O — TrimAllUserProcesses iterates every process on the system
        // and calls EmptyWorkingSet (which writes dirty pages). On a typical desktop that's
        // 5–15 seconds of work. Push it to the thread pool so the UI thread keeps painting
        // and the close/minimize buttons still respond while the trim runs.
        return Task.Run(() =>
        {
            var protectList = _protectList.Get();
            var attempted = protectList.Count == 0
                ? _processes.TrimAllUserProcesses()
                : _processes.TrimUserProcessesExcept(protectList);
            return TweakResult.Success($"{{\"attempted\":{attempted}}}");
        }, ct);
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows will repopulate working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will request Windows to trim working sets of all accessible processes.");
}
