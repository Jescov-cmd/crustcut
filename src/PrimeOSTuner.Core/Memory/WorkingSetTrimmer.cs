using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Memory;

public sealed class WorkingSetTrimmer : IWorkingSetTrimmer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, int flags);

    // ── Standby-list purge (NtSetSystemInformation) ──────────────────────────────────────
    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? host, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public uint Count; public LUID_AND_ATTRIBUTES Privilege; }

    private const int SystemMemoryListInformation = 0x0050; // 80
    private const int MemoryPurgeStandbyList = 4;
    private const uint SE_PRIVILEGE_ENABLED = 0x2;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x20, TOKEN_QUERY = 0x8;

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

    public void EmptyStandbyList()
    {
        // Enable the privilege the kernel requires to touch the memory lists. No-op (returns)
        // if we can't get it — e.g. not elevated.
        if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return;
        try
        {
            int command = MemoryPurgeStandbyList;
            NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
        }
        catch { /* unsupported OS / not permitted — ignore, it's best-effort */ }
    }

    private static bool EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, name, out var luid)) return false;
            var tp = new TOKEN_PRIVILEGES
            {
                Count = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
            };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch { return false; }
        finally { CloseHandle(token); }
    }
}
