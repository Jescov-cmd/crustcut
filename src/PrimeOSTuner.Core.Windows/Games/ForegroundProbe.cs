using System.Diagnostics;
using System.Runtime.InteropServices;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Windows.Games;

/// <summary>The Windows facts <see cref="AutoGameLearner"/> needs: the window in front and
/// how much of its monitor it covers, and what a process is called.</summary>
public static class ForegroundProbe
{
    public static ForegroundWindow? Read()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return null;
            if (GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0) return null;
            if (!GetWindowRect(hwnd, out var w)) return null;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return null;
            var m = info.rcMonitor;

            // Area of the window that lies on its monitor, over the monitor's area.
            var ix = Math.Max(0, Math.Min(w.Right, m.Right) - Math.Max(w.Left, m.Left));
            var iy = Math.Max(0, Math.Min(w.Bottom, m.Bottom) - Math.Max(w.Top, m.Top));
            var monitorArea = (double)(m.Right - m.Left) * (m.Bottom - m.Top);
            return monitorArea <= 0 ? null : new ForegroundWindow((int)pid, ix * (double)iy / monitorArea);
        }
        catch { return null; }
    }

    /// <summary>Limited-query access only: anti-cheat-protected games refuse the memory
    /// access Process.MainModule needs, and those are exactly the games worth finding.</summary>
    public static ProcessIdentity? Identify(int pid)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            var size = buffer.Length;
            if (!QueryFullProcessImageName(handle, 0, buffer, ref size)) return null;
            var path = new string(buffer, 0, size);
            string? product = null;
            try { product = FileVersionInfo.GetVersionInfo(path).ProductName; } catch { }
            return new ProcessIdentity(Path.GetFileName(path), path, product);
        }
        catch { return null; }
        finally { CloseHandle(handle); }
    }

    /// <summary>Exe name without extension ("Medal"), or null. One limited-access handle —
    /// Process.ProcessName instead snapshots every process on the machine to answer.</summary>
    public static string? ImageNameOf(int pid)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var chars = new char[1024];
            var size = chars.Length;
            return QueryFullProcessImageName(handle, 0, chars, ref size)
                ? Path.GetFileNameWithoutExtension(new string(chars, 0, size))
                : null;
        }
        finally { CloseHandle(handle); }
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, char[] buffer, ref int size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}
