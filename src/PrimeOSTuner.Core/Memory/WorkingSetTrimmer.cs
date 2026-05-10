using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Memory;

public sealed class WorkingSetTrimmer : IWorkingSetTrimmer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, int flags);

    public IReadOnlyList<ProcessSnapshot> Snapshot()
    {
        var snaps = new List<ProcessSnapshot>();
        foreach (var p in Process.GetProcesses())
        {
            try { snaps.Add(new ProcessSnapshot(p.Id, p.ProcessName, p.WorkingSet64)); }
            catch { }
            finally { p.Dispose(); }
        }
        return snaps;
    }

    public void TrimWorkingSet(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            EmptyWorkingSet(p.Handle);
        }
        catch { /* swallow — process may have exited */ }
    }

    public void FlushFileCache()
    {
        // -1, -1, 0 == release the standby file cache. Returns false if non-elevated; ignore.
        SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
    }
}
