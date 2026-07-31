using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Memory;

/// <summary>
/// Real standby-list access, same mechanism ISLC and Mem Reduct use:
/// NtSetSystemInformation(SystemMemoryListInformation, MemoryPurgeStandbyList) after
/// enabling SeProfileSingleProcessPrivilege. Sizes come from the Memory performance
/// counters.
/// </summary>
public sealed class StandbyListClient : IStandbyListClient
{
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const string PrivilegeName = "SeProfileSingleProcessPrivilege";

    [DllImport("ntdll.dll")]
    private static extern uint NtSetSystemInformation(int infoClass, ref int info, int length);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previous, IntPtr returnLength);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public int Count; public LUID Luid; public int Attributes; }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
    private const uint TOKEN_QUERY = 0x8;
    private const int SE_PRIVILEGE_ENABLED = 0x2;

    public long GetStandbyBytes() =>
        ReadCounter("Standby Cache Normal Priority Bytes")
        + ReadCounter("Standby Cache Reserve Bytes")
        + ReadCounter("Standby Cache Core Bytes");

    public long GetFreeBytes() => ReadCounter("Free & Zero Page List Bytes");

    // Rate counter: must be the SAME instance across reads — NextValue() returns the rate
    // since the previous call (first call is always 0). The engine polls once a minute,
    // which gives a solid average.
    private PerformanceCounter? _pageInput;

    public double GetPageInputPerSec()
    {
        try
        {
            _pageInput ??= new PerformanceCounter("Memory", "Pages Input/sec", readOnly: true);
            return _pageInput.NextValue();
        }
        catch
        {
            return 0;   // counter unavailable — thrash guard just never triggers
        }
    }

    public bool TryPurge()
    {
        try
        {
            if (!EnablePrivilege()) return false;
            var command = MemoryPurgeStandbyList;
            return NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static long ReadCounter(string counter)
    {
        try
        {
            using var c = new PerformanceCounter("Memory", counter, readOnly: true);
            return (long)c.NextValue();
        }
        catch
        {
            return 0;   // counter unavailable — gating math degrades to "don't purge"
        }
    }

    private static bool EnablePrivilege()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, PrivilegeName, out var luid)) return false;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) return false;
            // AdjustTokenPrivileges succeeds even when the privilege wasn't assigned;
            // ERROR_NOT_ALL_ASSIGNED (1300) reveals it. Elevated processes have it.
            return Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
