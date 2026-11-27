using System.Diagnostics;
using System.Runtime.InteropServices;
using PrimeOSTuner.Core.Safety;

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

    public bool TrySetMemoryLimit(int pid, int limitMb, bool hard = true)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (limitMb < 64) return false;   // below this even a small app thrashes; refuse
        try
        {
            using var p = Process.GetProcessById(pid);
            if (DeviceSoftware.IsProtected(p.ProcessName)) return false;   // never cap device software
            var ceiling = hard ? QUOTA_LIMITS_HARDWS_MAX_ENABLE : QUOTA_LIMITS_HARDWS_MAX_DISABLE;
            return SetProcessWorkingSetSizeEx(
                p.Handle,
                new IntPtr(4L * 1024 * 1024),
                new IntPtr((long)limitMb * 1024 * 1024),
                QUOTA_LIMITS_HARDWS_MIN_DISABLE | ceiling);
        }
        catch
        {
            return false;   // process gone or access denied
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private const int ProcessPowerThrottlingInfo = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess, int informationClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);

    public bool TrySetEfficiencyMode(int pid, bool on)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            if (on && DeviceSoftware.IsProtected(p.ProcessName)) return false;   // never throttle device software
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                // StateMask bit set = throttle on; control bit set with state 0 = force off.
                StateMask = on ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
            };
            var throttled = SetProcessInformation(
                p.Handle, ProcessPowerThrottlingInfo, ref state, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
            // Task Manager pairs EcoQoS with a lowered priority class; BelowNormal (not
            // Idle) so eco'd apps still make progress in the background.
            p.PriorityClass = on ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;
            return throttled;
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
            // Device software can be raised, never lowered.
            if (level == PriorityLevel.BelowNormal && DeviceSoftware.IsProtected(p.ProcessName)) return false;
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

    public string? ImagePathOf(int pid)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            var size = buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally { CloseHandle(handle); }
    }

    public IReadOnlyList<int> FindPidsForExes(IEnumerable<string> exePaths)
    {
        var set = new HashSet<string>(exePaths, StringComparer.OrdinalIgnoreCase);
        var pids = new List<int>();
        foreach (var (pid, path) in t_batch ?? SnapshotImagePaths())
            if (set.Contains(path)) pids.Add(pid);
        return pids;
    }

    // Per-thread so a sweep on one worker never hands a stale snapshot to a lookup on
    // another (the rule engine's process-start path must always see the live list).
    [ThreadStatic] private static List<(int Pid, string Path)>? t_batch;

    public IDisposable BeginBatch()
    {
        if (t_batch is not null) return new BatchScope(owner: false);   // nested: reuse
        t_batch = SnapshotImagePaths();
        return new BatchScope(owner: true);
    }

    private sealed class BatchScope(bool owner) : IDisposable
    {
        public void Dispose() { if (owner) t_batch = null; }
    }

    /// <summary>
    /// Every running process's full image path. QueryFullProcessImageName needs only
    /// PROCESS_QUERY_LIMITED_INFORMATION, so it reads processes that refuse the VM-read
    /// access Process.MainModule demands (anti-cheat-protected games among them), and it
    /// never walks module lists — MainModule enumerated modules for every process, and
    /// sweeps called that once per rule.
    /// </summary>
    private static List<(int Pid, string Path)> SnapshotImagePaths()
    {
        var result = new List<(int, string)>();
        var buffer = new char[1024];
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                if (handle == IntPtr.Zero) continue;   // access denied — system processes
                try
                {
                    var size = buffer.Length;
                    if (QueryFullProcessImageName(handle, 0, buffer, ref size))
                        result.Add((p.Id, new string(buffer, 0, size)));
                }
                finally { CloseHandle(handle); }
            }
            catch { /* exited mid-scan */ }
            finally { p.Dispose(); }
        }
        return result;
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, char[] buffer, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
