namespace PrimeOSTuner.Core.Memory;

public interface IPriorityClient
{
    /// <summary>Set CPU priority class on a process. Returns true on success, false if process is gone or access denied.</summary>
    bool TrySetPriority(int pid, PriorityLevel level);

    /// <summary>
    /// Hard-cap how much physical RAM a process may keep resident. The process keeps
    /// running; anything over the cap is paged out instead of held. Returns false if the
    /// process is gone, access is denied, or the platform has no such mechanism.
    /// </summary>
    bool TrySetMemoryLimit(int pid, int limitMb);

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
