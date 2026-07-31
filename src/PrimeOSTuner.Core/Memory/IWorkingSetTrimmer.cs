namespace PrimeOSTuner.Core.Memory;

/// <summary>
/// A point-in-time view of one running process. <paramref name="ParentPid"/> and
/// <paramref name="HasVisibleWindow"/> are what let the cleaner tell an application the
/// user is actively working in from a genuinely idle background process.
/// </summary>
/// <param name="Pid">Process id.</param>
/// <param name="Name">Process name without extension (e.g. "Code").</param>
/// <param name="WorkingSetBytes">Current working set size in bytes.</param>
/// <param name="ParentPid">Creating process id, or 0 when unknown.</param>
/// <param name="HasVisibleWindow">True when this process owns a visible top-level window (minimized counts).</param>
/// <param name="HasRestoredWindow">True when at least one of its visible windows is NOT minimized —
/// i.e. the app is actually on screen. Deep clean protects these but not minimized-only apps.</param>
public sealed record ProcessSnapshot(
    int Pid,
    string Name,
    long WorkingSetBytes,
    int ParentPid = 0,
    bool HasVisibleWindow = false,
    bool HasRestoredWindow = false);

public interface IWorkingSetTrimmer
{
    /// <summary>Enumerates every accessible process with the metadata the cleaner needs.</summary>
    IReadOnlyList<ProcessSnapshot> Snapshot();

    /// <summary>
    /// The process id owning the foreground window, or 0 when there is none. Kept on this
    /// interface rather than a separate one because it is the same OS process surface the
    /// cleaner already depends on, and it is only ever consulted to decide what to skip.
    /// </summary>
    int ForegroundPid();

    /// <summary>
    /// Asks Windows to evict <paramref name="pid"/>'s working set. This is destructive to a
    /// process in active use — every page it next touches becomes a hard fault from disk —
    /// so callers MUST filter through the cleaner's protection rules first.
    /// </summary>
    void TrimWorkingSet(int pid);
}
