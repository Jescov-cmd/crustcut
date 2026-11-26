using System.Diagnostics;
using Avalonia.Threading;

namespace Crustcut.App.Services;

/// <summary>
/// Automated tab-switch responsiveness test: `Crustcut.exe --navstress out.txt` opens the
/// window, clicks through the tabs the way a user would, and records how long the UI
/// thread stays busy after each switch — i.e. the freeze the user actually feels. Exists
/// because a startup screenshot can look fine while interactive navigation is still
/// seconds-laggy (that exact gap shipped one "fixed" build that wasn't).
/// </summary>
public static class NavStress
{
    public static string? Parse(string[] args)
    {
        var i = Array.IndexOf(args, "--navstress");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int SettleSeconds()
    {
        var i = Array.IndexOf(Program.Args, "--settle");
        return i >= 0 && i + 1 < Program.Args.Length && int.TryParse(Program.Args[i + 1], out var s)
            ? Math.Clamp(s, 0, 3600) : 30;
    }

    private static string Footprint(string label)
    {
        using var p = Process.GetCurrentProcess();
        var gc = GC.GetGCMemoryInfo();
        const double Mb = 1024 * 1024;
        return $"{label,-14} TASK MANAGER {PrivateWorkingSet() / Mb,5:F0} MB  private {p.PrivateMemorySize64 / Mb,6:F0} MB  " +
               $"working {p.WorkingSet64 / Mb,6:F0} MB  managed heap {gc.HeapSizeBytes / Mb,5:F0} MB  " +
               $"threads {p.Threads.Count,3}  handles {p.HandleCount,5}";
    }

    /// <summary>The number Task Manager's "Memory" column shows: private working set —
    /// our own pages actually sitting in RAM right now.</summary>
    private static long PrivateWorkingSet()
    {
        var c = new PROCESS_MEMORY_COUNTERS_EX2 { cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX2>() };
        using var p = Process.GetCurrentProcess();
        return GetProcessMemoryInfo(p.Handle, ref c, c.cb) ? (long)c.PrivateWorkingSetSize : 0;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS_EX2
    {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage,
            PrivateUsage, PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref PROCESS_MEMORY_COUNTERS_EX2 counters, uint size);

    public static async Task RunThenExitAsync(MainWindow window, string outPath, Action shutdown)
    {
        // Let startup work (engine, first probes) settle so it doesn't pollute the numbers.
        await Task.Delay(4000);

        // Each tab twice: first visit pays the one-time build+load, the revisit is what
        // day-to-day switching feels like.
        var sequence = new[]
        {
            "Memory", "Games", "Overview", "Optimize", "Diagnosis", "Cleanup", "Fans", "Sessions",
            "GameBoost", "Guides", "WhatsNew", "History", "Settings",
            "Memory", "Games", "Overview", "Memory", "Games",
        };

        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tab in sequence)
        {
            var kind = seen.Add(tab) ? "first" : "revisit";
            var heapBefore = kind == "first" ? GC.GetTotalMemory(forceFullCollection: true) : 0;
            var sw = Stopwatch.StartNew();
            window.NavigateTo(tab);
            // Background priority runs only after all layout/render work drains, so the
            // elapsed time ≈ how long the UI thread was pinned by the switch.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            sw.Stop();
            var cost = "";
            if (kind == "first")
            {
                await Task.Delay(1500);   // let the page's first load finish
                cost = $"  +{(GC.GetTotalMemory(forceFullCollection: true) - heapBefore) / 1048576.0,5:F1} MB managed";
            }
            lines.Add($"{tab,-10} {kind,-8} {sw.ElapsedMilliseconds,5} ms{cost}");
            await Task.Delay(500);
        }

        // With every tab visited and the window still open: what Task Manager shows while
        // someone is using the app.
        await Task.Delay(2000);
        lines.Add("");
        lines.Add(Footprint("window open"));
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        await Task.Delay(500);
        lines.Add(Footprint("open, after GC"));
        // "--hold N": keep the window open N more seconds (for attaching a heap snapshot).
        var hold = Array.IndexOf(Program.Args, "--hold");
        if (hold >= 0 && hold + 1 < Program.Args.Length && int.TryParse(Program.Args[hold + 1], out var holdS))
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(holdS, 0, 600)));

        // Then the footprint the user actually lives with: window closed to the tray, the
        // engine idling. "--settle N" sets how long to idle first (default 30 s). Measured
        // twice — as-is, then after a full blocking GC — so live data and uncollected
        // garbage can be told apart.
        window.Close();          // exactly what the X does with close-to-tray on
        window = null!;          // drop our reference so the closed window can be freed
        await Task.Delay(TimeSpan.FromSeconds(SettleSeconds()));
        lines.Add("");
        lines.Add(Footprint("tray idle"));
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        await Task.Delay(1000);
        lines.Add(Footprint("after full GC"));

        try { File.WriteAllLines(outPath, lines); } catch { }
        shutdown();
    }
}
