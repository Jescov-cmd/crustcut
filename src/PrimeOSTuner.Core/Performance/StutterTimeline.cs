namespace PrimeOSTuner.Core.Performance;

/// <summary>An app holding a lot of memory at the moment stutters were happening.</summary>
public sealed record MemoryHog(string Name, long Bytes);

/// <summary>
/// What a game session's stutters had in common. Counts are in seconds of play: a second
/// "stuttered" when its worst frame took far longer than the session's typical frame.
/// </summary>
public sealed record StutterReport(
    int SecondsAnalyzed,
    int StutterSeconds,
    int MemorySeconds,
    int GpuSeconds,
    int CpuSeconds,
    double PeakRamPercent,
    IReadOnlyList<MemoryHog> TopApps,
    string Verdict,
    int DriverSeconds = 0,
    double PeakDriverPercent = 0);

/// <summary>
/// Lines up every second of a game session: the worst frame the game drew in that second
/// against what the machine was doing at the time (RAM, disk paging, CPU, GPU). A 1%-low
/// number says THAT a game stuttered; this says WHY, in terms a player can act on —
/// "Windows was pulling memory back off the disk while VS Code held 3 GB" is a fix,
/// "1% low: 11 fps" is not.
///
/// Pure logic; no clock, no OS calls — callers pass timestamps and readings in.
/// </summary>
public sealed class StutterTimeline
{
    // A second counts as a stutter when its worst frame is this many times the session's
    // median frame, and never below ~2 frames at 60 fps (so a 240 fps game dipping to
    // 120 isn't called a stutter).
    private const double StutterFactor = 2.5;
    private const double StutterFloorMs = 33.4;

    // What "the machine was the problem" means for one second.
    private const double PagingPagesPerSec = 400;   // sustained reads of memory back off disk
    private const double RamFullPercent = 92;
    private const double RamTightPercent = 85;   // paging only counts as memory pressure above this
    private const double GpuMaxedPercent = 97;
    private const double CpuMaxedPercent = 90;
    // A healthy core spends well under 2% of its time in drivers. Past this, one driver
    // is holding a core hostage in bursts long enough to delay input, frames and packets.
    private const double DriverHogPercent = 5;

    private const int MinSecondsForReport = 30;
    private const int MinStuttersForCause = 3;

    private readonly DateTime _startUtc;
    private readonly Dictionary<long, double> _worstFrameMs = new();
    private readonly Dictionary<long, SystemPoint> _system = new();

    private readonly record struct SystemPoint(double Cpu, double Gpu, double Ram, double PageIn, double CoreDpc);

    public StutterTimeline(DateTime startUtc) => _startUtc = startUtc;

    private long Second(DateTime utc) => (long)Math.Floor((utc - _startUtc).TotalSeconds);

    public void AddFrame(DateTime utc, double frameMs)
    {
        var s = Second(utc);
        if (!_worstFrameMs.TryGetValue(s, out var worst) || frameMs > worst) _worstFrameMs[s] = frameMs;
    }

    /// <summary>One system reading. Readings describe the second just before them.</summary>
    public void AddSystem(DateTime utc, double cpuPercent, double gpuPercent, double ramPercent, double pageInPerSec,
        double maxCoreDpcPercent = 0)
        => _system[Second(utc)] = new SystemPoint(cpuPercent, gpuPercent, ramPercent, pageInPerSec, maxCoreDpcPercent);

    /// <summary>True when a frame in the last couple of seconds was a hitch by any
    /// standard — used to decide when it's worth noting who holds the memory.</summary>
    public bool StutteredRecently(DateTime utc, double thresholdMs = 50)
    {
        var s = Second(utc);
        for (var k = s - 2; k <= s; k++)
            if (_worstFrameMs.TryGetValue(k, out var w) && w >= thresholdMs) return true;
        return false;
    }

    public StutterReport Analyze(IReadOnlyList<double> allFrameTimesMs, IReadOnlyList<MemoryHog> topApps,
        long? gameHeapBytes = null, long totalRamBytes = 0)
    {
        var seconds = _worstFrameMs.Count;
        var median = Median(allFrameTimesMs);
        var threshold = Math.Max(StutterFloorMs, median * StutterFactor);

        int stutters = 0, memory = 0, driver = 0, gpu = 0, cpu = 0;
        double peakRam = 0, peakDriver = 0;
        foreach (var (second, worst) in _worstFrameMs)
        {
            if (worst < threshold) continue;
            stutters++;
            // The reading taken just after this second describes it; fall back to the one
            // at the same index when the sampler and the frames land a tick apart.
            if (!_system.TryGetValue(second + 1, out var sys) && !_system.TryGetValue(second, out sys))
                continue;
            peakRam = Math.Max(peakRam, sys.Ram);
            peakDriver = Math.Max(peakDriver, sys.CoreDpc);
            // Page-ins alone don't mean RAM ran out: "Pages Input/sec" also counts programs
            // and game files being read in (launches, loading screens). Only with RAM
            // actually tight is heavy paging evidence of memory pressure.
            if (sys.Ram >= RamFullPercent || (sys.PageIn >= PagingPagesPerSec && sys.Ram >= RamTightPercent)) memory++;
            // Paging first: reading memory back off disk ALSO shows up as driver time (the
            // storage driver), so a paging second must not be blamed on a driver.
            else if (sys.CoreDpc >= DriverHogPercent) driver++;
            else if (sys.Gpu >= GpuMaxedPercent) gpu++;
            else if (sys.Cpu >= CpuMaxedPercent) cpu++;
        }

        var verdict = Explain(seconds, stutters, memory, gpu, cpu, peakRam, topApps, driver, peakDriver,
            gameHeapBytes, totalRamBytes);
        return new StutterReport(seconds, stutters, memory, gpu, cpu, peakRam, topApps, verdict, driver, peakDriver);
    }

    public static string Explain(int seconds, int stutters, int memory, int gpu, int cpu,
        double peakRam, IReadOnlyList<MemoryHog> topApps, int driver = 0, double peakDriver = 0,
        long? gameHeapBytes = null, long totalRamBytes = 0)
    {
        if (seconds < MinSecondsForReport)
            return "Too short to judge.";
        if (stutters < MinStuttersForCause)
            return stutters == 0 ? "Smooth — no stutters." : "Smooth — only a hitch or two.";

        var dominant = Math.Max(Math.Max(memory, driver), Math.Max(gpu, cpu));
        if (dominant * 2 < stutters)
            return $"{stutters} stutters with no single cause on the PC side — memory, graphics card, " +
                   "processor and drivers all had room. That usually points at the game itself (loading " +
                   "new areas, or its own clean-up pauses).";

        if (dominant == driver)
            return $"{driver} of {stutters} stutters hit while a driver was hogging one of your processor " +
                   $"cores (up to {peakDriver:F0}% of its time). While that happens, input, frames and " +
                   "network packets all wait — it feels like lag. It's usually the network card: turn on " +
                   "\"Network card latency fix\" on the Optimize tab.";

        if (dominant == memory)
        {
            var hogs = topApps.Count == 0 ? "" :
                " Biggest memory users at the time: " +
                string.Join(", ", topApps.Take(3).Select(h => $"{h.Name} {Gb(h.Bytes)}")) + ".";
            return $"{memory} of {stutters} stutters hit while your RAM was full (up to {peakRam:F0}%). " +
                   HeapAdvice(gameHeapBytes, totalRamBytes) +
                   $"Closing big apps before you play is the other fix.{hogs}";
        }
        if (dominant == gpu)
            return $"{gpu} of {stutters} stutters hit while your graphics card was maxed out. Lower the " +
                   "resolution or view distance, or cap your frame rate a little below where it runs.";
        return $"{cpu} of {stutters} stutters hit while your processor was maxed out. Close background " +
               "apps, or lower settings the processor pays for (view/simulation distance, entity counts).";
    }

    /// <summary>
    /// For games that reserve their own memory up front (Java Minecraft's launcher slider,
    /// passed as -Xmx): if it's more than ~40% of the PC's RAM, it's starving everything
    /// else — including Windows' file cache the game itself loads from. Suggests about a
    /// third of RAM, never below the 4 GB modded Minecraft generally needs.
    /// </summary>
    public static string HeapAdvice(long? gameHeapBytes, long totalRamBytes)
    {
        if (gameHeapBytes is not long heap || totalRamBytes <= 0) return "";
        if (heap <= totalRamBytes * 0.4) return "";
        var suggestGb = Math.Max(4, (int)Math.Round(totalRamBytes * 0.35 / (1L << 30)));
        var heapGb = heap / (double)(1L << 30);
        if (suggestGb >= heapGb) return "";
        return $"The game itself is allowed up to {heapGb:0.#} GB of your {totalRamBytes / (double)(1L << 30):F0} GB " +
               $"(the memory slider in your launcher) — try {suggestGb} GB. ";
    }

    private static string Gb(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):F1} GB" : $"{bytes >> 20} MB";

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.ToArray();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }
}
