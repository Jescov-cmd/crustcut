namespace PrimeOSTuner.Core.Memory;

public sealed record ProcessSnapshot(int Pid, string Name, long WorkingSetBytes);

public interface IWorkingSetTrimmer
{
    IReadOnlyList<ProcessSnapshot> Snapshot();
    void TrimWorkingSet(int pid);
    void FlushFileCache();

    /// <summary>
    /// Purges the system standby (cached) memory list — RAM Windows is holding as file/page
    /// cache "just in case". This is the big RAM-freeing step (what tools like ISLC do).
    /// Needs elevation; a no-op if not elevated. Windows re-caches on demand, so it's safe.
    /// </summary>
    void EmptyStandbyList();
}
