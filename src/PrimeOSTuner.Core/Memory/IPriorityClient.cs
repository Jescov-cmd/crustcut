namespace PrimeOSTuner.Core.Memory;

public interface IPriorityClient
{
    /// <summary>Set CPU priority class on a process. Returns true on success, false if process is gone or access denied.</summary>
    bool TrySetPriority(int pid, PriorityLevel level);

    /// <summary>
    /// Cap how much physical RAM a process may keep resident. The process keeps running;
    /// anything over the cap is paged out instead of held. Returns false if the process is
    /// gone, access is denied, or the platform has no such mechanism.
    ///
    /// <paramref name="hard"/> decides how the ceiling behaves, and the difference matters
    /// more than it looks. A HARD cap makes Windows evict the moment the process reaches
    /// it — on anything that draws, the evicted pages include render surfaces, which the
    /// user sees as black rectangles and blurry text. A SOFT cap is a hint: Windows trims
    /// towards it under memory pressure and otherwise leaves the process alone. Anything
    /// Crustcut decides by itself must be soft; only a cap the user chose per-app earns a
    /// hard ceiling.
    /// </summary>
    bool TrySetMemoryLimit(int pid, int limitMb, bool hard = true);

    /// <summary>Remove a previously applied memory cap. Best-effort; false on failure.</summary>
    bool TryClearMemoryLimit(int pid);

    /// <summary>
    /// Windows 11 Efficiency Mode (EcoQoS + lowered priority) — the Task Manager leaf.
    /// The process keeps running but the scheduler treats it as a background citizen:
    /// efficiency cores, reduced clocks, minimal interference with what the user is doing.
    /// Cleared automatically when the process exits; call with on=false to lift it early.
    /// </summary>
    bool TrySetEfficiencyMode(int pid, bool on);

    /// <summary>Returns PIDs whose main module path matches one of the given EXE paths (case-insensitive).</summary>
    IReadOnlyList<int> FindPidsForExe(string exePath);

    /// <summary>Returns currently running PIDs whose main module path matches any in the protect list. Used by SafeRamCleaner.</summary>
    IReadOnlyList<int> FindPidsForExes(IEnumerable<string> exePaths);
}
