using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Memory;

public sealed class WorkingSetTrimmer : IWorkingSetTrimmer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr param);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    public IReadOnlyList<ProcessSnapshot> Snapshot()
    {
        var parents = ReadParentPids();
        var windowed = ReadWindowedPids();

        var snaps = new List<ProcessSnapshot>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                snaps.Add(new ProcessSnapshot(
                    p.Id,
                    p.ProcessName,
                    p.WorkingSet64,
                    parents.TryGetValue(p.Id, out var parent) ? parent : 0,
                    windowed.Contains(p.Id)));
            }
            catch { /* process exited between enumeration and read */ }
            finally { p.Dispose(); }
        }
        return snaps;
    }

    public int ForegroundPid()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return 0;
        return GetWindowThreadProcessId(hWnd, out var pid) == 0 ? 0 : (int)pid;
    }

    public void TrimWorkingSet(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            EmptyWorkingSet(p.Handle);
        }
        catch { /* swallow — process may have exited, or refuses access */ }
    }

    /// <summary>Maps child pid → parent pid for every process, via the toolhelp snapshot.</summary>
    private static Dictionary<int, int> ReadParentPids()
    {
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle || snapshot == IntPtr.Zero) return map;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return map;
            do
            {
                map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            }
            while (Process32Next(snapshot, ref entry));
        }
        catch { /* best effort — an empty map just means less protection data */ }
        finally { CloseHandle(snapshot); }

        return map;
    }

    /// <summary>Every pid owning at least one visible top-level window.</summary>
    private static HashSet<int> ReadWindowedPids()
    {
        var pids = new HashSet<int>();
        try
        {
            EnumWindows((hWnd, _) =>
            {
                if (IsWindowVisible(hWnd) && GetWindowThreadProcessId(hWnd, out var pid) != 0)
                    pids.Add((int)pid);
                return true; // keep enumerating
            }, IntPtr.Zero);
        }
        catch { /* best effort */ }
        return pids;
    }
}
