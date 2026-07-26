namespace PrimeOSTuner.Core.Memory;

/// <summary>
/// Trims working sets of genuinely idle background processes, while never touching an
/// application the user is working in. Pure logic — all OS access goes through
/// <see cref="IWorkingSetTrimmer"/> so the protection rules are unit-testable.
/// </summary>
public sealed class SafeRamCleaner
{
    private const long MinWorkingSetThreshold = 100L * 1024 * 1024; // 100 MB

    // Shell and critical OS infrastructure. Trimming these is never worth it.
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

    /// <summary>
    /// Trims every process that is above the size threshold and not protected.
    /// Returns how many processes were trimmed.
    /// </summary>
    /// <param name="launchingPid">A process to always protect (e.g. the game being started).</param>
    /// <param name="protectedPids">Additional pids the user has explicitly protected.</param>
    public Task<int> RunAsync(int launchingPid, IEnumerable<int> protectedPids, CancellationToken ct = default)
    {
        var snapshot = _trimmer.Snapshot();
        var protectedSet = ComputeProtectedPids(
            snapshot, _trimmer.ForegroundPid(), protectedPids, launchingPid);

        var trimmed = 0;
        foreach (var s in snapshot)
        {
            if (ct.IsCancellationRequested) break;
            // Pid 0 is the System Idle Process. It deliberately never enters the protected
            // set (seeding it would make every orphan look like its child), so it has to be
            // excluded here instead.
            if (s.Pid == 0) continue;
            if (protectedSet.Contains(s.Pid)) continue;
            if (s.WorkingSetBytes < MinWorkingSetThreshold) continue;
            _trimmer.TrimWorkingSet(s.Pid);
            trimmed++;
        }

        return Task.FromResult(trimmed);
    }

    /// <summary>
    /// Builds the set of pids that must not be trimmed. Protection is derived from process
    /// structure rather than a hardcoded application list, so it works for apps we've never
    /// heard of.
    /// </summary>
    private static HashSet<int> ComputeProtectedPids(
        IReadOnlyList<ProcessSnapshot> snapshot,
        int foregroundPid,
        IEnumerable<int> explicitlyProtected,
        int launchingPid)
    {
        // ── Seeds: processes protected in their own right ────────────────────────────────
        var seeds = new HashSet<int>();

        void Seed(int pid)
        {
            // Pid 0 is the System Idle Process. Seeding it would make every orphaned
            // process (which reports ParentPid 0) look like its child.
            if (pid != 0) seeds.Add(pid);
        }

        foreach (var pid in explicitlyProtected) Seed(pid);
        Seed(launchingPid);
        Seed(foregroundPid);
        Seed(Environment.ProcessId);            // never trim Crustcut itself

        foreach (var s in snapshot)
        {
            if (s.HasVisibleWindow) Seed(s.Pid);              // an app the user can see
            if (SystemProcessNames.Contains(s.Name)) Seed(s.Pid);
        }

        // ── Rule: anything sharing an executable name with a seed is the same app ────────
        var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in snapshot)
            if (seeds.Contains(s.Pid)) protectedNames.Add(s.Name);

        foreach (var s in snapshot)
            if (protectedNames.Contains(s.Name)) Seed(s.Pid);

        // ── Rule: descendants of a protected process are protected too ──────────────────
        // This is the rule that fixes the VS Code case: renderers, the extension host and
        // language servers own no windows, so nothing else would protect them, yet trimming
        // them is exactly what makes the editor stall.
        var childrenByParent = new Dictionary<int, List<int>>();
        foreach (var s in snapshot)
        {
            if (s.ParentPid == 0) continue;     // orphan — no usable parent link
            if (!childrenByParent.TryGetValue(s.ParentPid, out var kids))
                childrenByParent[s.ParentPid] = kids = new List<int>();
            kids.Add(s.Pid);
        }

        var result = new HashSet<int>(seeds);
        var queue = new Queue<int>(seeds);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            if (!childrenByParent.TryGetValue(pid, out var kids)) continue;
            foreach (var kid in kids)
            {
                // HashSet.Add returns false if already present, which is what stops a cycle
                // caused by recycled pids from looping forever.
                if (result.Add(kid)) queue.Enqueue(kid);
            }
        }

        return result;
    }
}
