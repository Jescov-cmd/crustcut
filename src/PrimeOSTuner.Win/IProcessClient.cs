namespace PrimeOSTuner.Win;

public interface IProcessClient
{
    void TrimWorkingSet(int processId);
    int TrimAllUserProcesses();

    /// <summary>
    /// Trim working sets of all user processes EXCEPT those whose main module path is
    /// in <paramref name="exePathExclusions"/> (case-insensitive).
    /// Returns the number of processes whose trim was attempted.
    /// </summary>
    int TrimUserProcessesExcept(IReadOnlyCollection<string> exePathExclusions);
}
