using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Memory;

public sealed class PriorityClient : IPriorityClient
{
    // SetProcessWorkingSetSizeEx with a HARD max is the same OS lever Edge-style
    // "resource controls" use: the process keeps running, but Windows refuses to let its
    // resident set grow past the cap — the excess lives in the pagefile instead of RAM.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSizeEx(
        IntPtr hProcess, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize, uint flags);

    private const uint QUOTA_LIMITS_HARDWS_MIN_DISABLE = 0x2;   // min stays advisory
    private const uint QUOTA_LIMITS_HARDWS_MAX_ENABLE  = 0x4;   // max becomes a HARD cap
    private const uint QUOTA_LIMITS_HARDWS_MAX_DISABLE = 0x8;   // max back to advisory

    public bool TrySetMemoryLimit(int pid, int limitMb)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (limitMb < 64) return false;   // below this even a small app thrashes; refuse
        try
        {
            using var p = Process.GetProcessById(pid);
            return SetProcessWorkingSetSizeEx(
                p.Handle,
                new IntPtr(4L * 1024 * 1024),
                new IntPtr((long)limitMb * 1024 * 1024),
                QUOTA_LIMITS_HARDWS_MIN_DISABLE | QUOTA_LIMITS_HARDWS_MAX_ENABLE);
        }
        catch
        {
            return false;   // process gone or access denied
        }
    }

    public bool TryClearMemoryLimit(int pid)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            // Values become advisory once both DISABLE flags are set — effectively uncapped.
            return SetProcessWorkingSetSizeEx(
                p.Handle,
                new IntPtr(4L * 1024 * 1024),
                new IntPtr(32L * 1024 * 1024 * 1024),
                QUOTA_LIMITS_HARDWS_MIN_DISABLE | QUOTA_LIMITS_HARDWS_MAX_DISABLE);
        }
        catch
        {
            return false;
        }
    }

    public bool TrySetPriority(int pid, PriorityLevel level)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.PriorityClass = level switch
            {
                PriorityLevel.High => ProcessPriorityClass.High,
                PriorityLevel.AboveNormal => ProcessPriorityClass.AboveNormal,
                PriorityLevel.Normal => ProcessPriorityClass.Normal,
                PriorityLevel.BelowNormal => ProcessPriorityClass.BelowNormal,
                _ => ProcessPriorityClass.Normal
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<int> FindPidsForExe(string exePath)
        => FindPidsForExes(new[] { exePath });

    public IReadOnlyList<int> FindPidsForExes(IEnumerable<string> exePaths)
    {
        var set = new HashSet<string>(exePaths, StringComparer.OrdinalIgnoreCase);
        var pids = new List<int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (path is not null && set.Contains(path))
                    pids.Add(p.Id);
            }
            catch
            {
                // Access denied to query module path — common for system processes.
            }
            finally
            {
                p.Dispose();
            }
        }
        return pids;
    }
}
