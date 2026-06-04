namespace PrimeOSTuner.Core.Memory;

public sealed class SafeRamCleaner
{
    private const long MinWorkingSetThreshold = 100L * 1024 * 1024; // 100 MB

    // Process names we never trim — shell + critical OS infrastructure.
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression",
        "csrss", "wininit", "services", "lsass", "winlogon",
        "smss", "dwm", "audiodg", "explorer", "fontdrvhost",
        "RuntimeBroker", "sihost", "taskhostw"
    };

    private readonly IWorkingSetTrimmer _trimmer;

    public SafeRamCleaner(IWorkingSetTrimmer trimmer)
    {
        _trimmer = trimmer;
    }

    public Task RunAsync(int launchingPid, IEnumerable<int> protectedPids, CancellationToken ct = default)
    {
        var protectedSet = new HashSet<int>(protectedPids) { launchingPid };
        var snapshot = _trimmer.Snapshot();

        foreach (var s in snapshot)
        {
            if (ct.IsCancellationRequested) break;
            if (protectedSet.Contains(s.Pid)) continue;
            if (SystemProcessNames.Contains(s.Name)) continue;
            if (s.WorkingSetBytes < MinWorkingSetThreshold) continue;
            _trimmer.TrimWorkingSet(s.Pid);
        }

        _trimmer.FlushFileCache();
        // Free the standby (cached) list too — this is the big RAM-reclaim step, releasing
        // memory Windows is holding as "just in case" file cache for things you're not using.
        _trimmer.EmptyStandbyList();
        return Task.CompletedTask;
    }
}
