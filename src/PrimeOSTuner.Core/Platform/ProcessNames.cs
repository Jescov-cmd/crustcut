using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Platform;

/// <summary>
/// Every running exe name ("javaw.exe"), read the cheap way. On Windows this is one
/// toolhelp snapshot: Process.GetProcesses() built ~400 Process objects (each a handle
/// waiting for a finalizer) just to read their names, every couple of seconds.
/// </summary>
public static class ProcessNames
{
    /// <summary>Throws when the list can't be read, so callers can skip a tick rather than
    /// conclude that nothing is running.</summary>
    public static string[] Snapshot()
    {
        var names = OperatingSystem.IsWindows() ? Toolhelp() : Managed();
        return names.Length > 0 ? names : throw new InvalidOperationException("process list unreadable");
    }

    private static string[] Managed() =>
        Process.GetProcesses()
            .Select(p => { try { return p.ProcessName + ".exe"; } catch { return ""; } finally { p.Dispose(); } })
            .Where(s => s.Length > 4)
            .ToArray();

    private static string[] Toolhelp()
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle) return Managed();
        try
        {
            var names = new List<string>(512);
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return Managed();
            do
            {
                if (entry.th32ProcessID != 0 && !string.IsNullOrEmpty(entry.szExeFile))
                    names.Add(entry.szExeFile);
            } while (Process32Next(snapshot, ref entry));
            return names.ToArray();
        }
        finally { CloseHandle(snapshot); }
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
    private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
    private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

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
}
