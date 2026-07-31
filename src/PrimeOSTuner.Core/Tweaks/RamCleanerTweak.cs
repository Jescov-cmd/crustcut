using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : IOneShotTweak
{
    private readonly SafeRamCleaner _cleaner;
    private readonly IRamCleanerProtectList _protectList;
    private readonly IPriorityClient _priority;
    private readonly RamCleanMode _mode;

    public string Id => _mode == RamCleanMode.Deep ? "core.ram-cleaner-deep" : "core.ram-cleaner";
    public string DisplayName => _mode == RamCleanMode.Deep ? "Deep clean RAM" : "Free up RAM now";

    // Deliberately plain about the trade-off. The previous copy called this "safe" while it
    // was purging the machine-wide standby list, which made the whole system slower.
    public string Description => _mode == RamCleanMode.Deep
        ? "Bigger cleanup: also releases memory from MINIMIZED apps and smaller background " +
          "programs. Minimized apps take a moment to wake when you return to them. The app " +
          "in focus, anything visible on screen, and PROTECT-ed apps are never touched."
        : "Releases memory held by idle background programs. Apps you're using — anything with " +
          "an open window, whatever's in focus, and their helper processes — are left alone. " +
          "Trimmed programs may pause briefly the next time you switch to them while Windows " +
          "reloads what they need.";

    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public RamCleanerTweak(SafeRamCleaner cleaner, IRamCleanerProtectList protectList,
        IPriorityClient priority, RamCleanMode mode = RamCleanMode.Normal)
    {
        _cleaner = cleaner;
        _protectList = protectList;
        _priority = priority;
        _mode = mode;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // Enumerating every process and trimming is heavy synchronous work; keep it off the
        // UI thread so the window stays responsive.
        var report = await Task.Run(() =>
        {
            var protectedPids = _priority.FindPidsForExes(_protectList.Get());
            // launchingPid 0 == nothing extra to protect beyond the structural rules.
            return _cleaner.RunAsync(0, protectedPids, _mode, ct);
        }, ct);

        // The message is the whole point — a silent cleanup is indistinguishable from a
        // broken one. Freed is a real measurement (working-set delta), not an estimate.
        RamCleanLog.TryWrite(report);
        var freedMb = report.FreedBytes / (1024 * 1024);
        var message = report.Trimmed == 0
            ? "Nothing to clean — everything running is either in use or already lean."
            : $"Freed {freedMb} MB from {report.Trimmed} background app(s).";
        return TweakResult.Success($"{{\"trimmed\":{report.Trimmed}}}", message);
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows repopulates working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will trim idle background programs, leaving apps you're using untouched.");
}
