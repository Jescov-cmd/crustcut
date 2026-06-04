using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly IProcessClient _processes;
    private readonly IRamCleanerProtectList _protectList;
    private readonly IWorkingSetTrimmer _trimmer;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Free up RAM now";
    public string Description => "Trims every app's working set, flushes the file cache, and purges the standby (cached) memory list — releasing RAM Windows is holding for things you're not using. Windows re-caches on demand, so it's safe; great after closing a game or heavy app.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public RamCleanerTweak(IProcessClient processes, IRamCleanerProtectList protectList, IWorkingSetTrimmer trimmer)
    {
        _processes = processes;
        _protectList = protectList;
        _trimmer = trimmer;
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
            // The big RAM-reclaim step: drop the file cache + standby (cached) list.
            _trimmer.FlushFileCache();
            _trimmer.EmptyStandbyList();
            return TweakResult.Success($"{{\"attempted\":{attempted}}}");
        }, ct);
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows will repopulate working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will request Windows to trim working sets of all accessible processes.");
}
