namespace PrimeOSTuner.Core.Memory;

public interface IPriorityClient
{
    /// <summary>Set CPU priority class on a process. Returns true on success, false if process is gone or access denied.</summary>
    bool TrySetPriority(int pid, PriorityLevel level);

    /// <summary>Returns PIDs whose main module path matches one of the given EXE paths (case-insensitive).</summary>
    IReadOnlyList<int> FindPidsForExe(string exePath);

    /// <summary>Returns currently running PIDs whose main module path matches any in the protect list. Used by SafeRamCleaner.</summary>
    IReadOnlyList<int> FindPidsForExes(IEnumerable<string> exePaths);
}
