using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly SafeRamCleaner _cleaner;
    private readonly IRamCleanerProtectList _protectList;
    private readonly IPriorityClient _priority;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Free up RAM now";

    // Deliberately plain about the trade-off. The previous copy called this "safe" while it
    // was purging the machine-wide standby list, which made the whole system slower.
    public string Description =>
        "Releases memory held by idle background programs. Apps you're using — anything with " +
        "an open window, whatever's in focus, and their helper processes — are left alone. " +
        "Trimmed programs may pause briefly the next time you switch to them while Windows " +
        "reloads what they need.";

    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public RamCleanerTweak(SafeRamCleaner cleaner, IRamCleanerProtectList protectList, IPriorityClient priority)
    {
        _cleaner = cleaner;
        _protectList = protectList;
        _priority = priority;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // Enumerating every process and trimming is heavy synchronous work; keep it off the
        // UI thread so the window stays responsive.
        var trimmed = await Task.Run(() =>
        {
            var protectedPids = _priority.FindPidsForExes(_protectList.Get());
            // launchingPid 0 == nothing extra to protect beyond the structural rules.
            return _cleaner.RunAsync(0, protectedPids, ct);
        }, ct);

        return TweakResult.Success($"{{\"trimmed\":{trimmed}}}");
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows repopulates working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will trim idle background programs, leaving apps you're using untouched.");
}
