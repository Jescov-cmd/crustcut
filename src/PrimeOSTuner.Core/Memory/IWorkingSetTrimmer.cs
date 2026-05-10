namespace PrimeOSTuner.Core.Memory;

public sealed record ProcessSnapshot(int Pid, string Name, long WorkingSetBytes);

public interface IWorkingSetTrimmer
{
    IReadOnlyList<ProcessSnapshot> Snapshot();
    void TrimWorkingSet(int pid);
    void FlushFileCache();
}
