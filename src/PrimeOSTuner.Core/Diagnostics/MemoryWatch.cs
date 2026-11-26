using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Diagnostics;

/// <summary>
/// Keeps Crustcut's own memory honest, and explains it in the log.
///
/// The problem it exists for: running as admin on the user's machine, Crustcut's private
/// memory climbed ~15 MB/s to 400–860 MB and fell back to ~100 MB every ~23 s — exactly
/// when .NET ran its routine gen-0 cleanup — while the .NET heap itself stayed under 35 MB.
/// Something allocates native memory that is only released when the object owning it is
/// cleaned up, and .NET has no idea that tiny object is holding megabytes, so it waits.
///
/// Two parts:
/// - <see cref="CollectIfNativeGrew"/> (every couple of seconds): when private memory has
///   grown well past where the last cleanup left it, clean up now instead of in 20 s.
///   A gen-0 collection of a heap this small costs about a millisecond. If gen 0 doesn't
///   give the memory back, the next one goes a generation deeper.
/// - <see cref="Track"/> around each background job, so <see cref="Report"/> can name the
///   job the growth happened during. Other threads add noise, but a job allocating tens
///   of MB a second dominates wherever it runs.
/// </summary>
public static class MemoryWatch
{
    /// <summary>Growth past the last cleanup that is worth an early one.</summary>
    public const long CollectThresholdBytes = 64L << 20;

    private static readonly ConcurrentDictionary<string, long> Growth = new();
    private static readonly object Gate = new();
    private static long _baseline;          // private bytes right after the last cleanup
    private static long _lastSeen;
    private static long _grewTotal;         // sum of rises between checks this interval
    private static long _peak;
    private static int _generation;         // how deep early cleanups go (0..2)
    private static int _cleanups;
    private static long _returned;

    /// <summary>Measures one unit of work. Use as <c>using var _ = MemoryWatch.Track("name");</c></summary>
    public static Scope Track(string job) => new(job, PrivateBytes());

    public readonly struct Scope(string job, long before) : IDisposable
    {
        public void Dispose()
        {
            if (before == 0) return;
            var grew = PrivateBytes() - before;
            if (grew > 0) Growth.AddOrUpdate(job, grew, (_, v) => v + grew);
        }
    }

    /// <summary>Call every couple of seconds. Returns a log line the first time an early
    /// cleanup happens (so the log shows it working), otherwise null.</summary>
    public static string? CollectIfNativeGrew()
    {
        lock (Gate)
        {
            var now = PrivateBytes();
            if (now == 0) return null;
            if (_lastSeen != 0 && now > _lastSeen) _grewTotal += now - _lastSeen;
            _lastSeen = now;
            _peak = Math.Max(_peak, now);
            if (_baseline == 0 || now < _baseline) { _baseline = now; return null; }
            if (now - _baseline < CollectThresholdBytes) return null;

            var gen = _generation;
            GC.Collect(gen, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();   // the native memory goes back as finalizers run
            var after = PrivateBytes();
            var back = Math.Max(0, now - after);
            // Didn't give back at least a quarter of the growth: the owners live longer than
            // gen 0 — go deeper next time. Never deeper than needed: a full collection is
            // still cheap here, but there's no reason to do more work than the problem asks.
            if (back < (now - _baseline) / 4 && _generation < 2) _generation++;
            _baseline = after;
            _lastSeen = after;
            _cleanups++;
            _returned += back;
            return _cleanups == 1
                ? $"memory: early cleanup gave back {Mb(back)} ({Mb(now)} → {Mb(after)}, gen {gen})"
                : null;
        }
    }

    /// <summary>One log line for the interval since the last report, then counters reset.
    /// Null on the first call (it only takes a baseline).</summary>
    public static string? Report()
    {
        lock (Gate)
        {
            var now = PrivateBytes();
            var jobs = Growth.ToArray();
            Growth.Clear();
            var first = _peak == 0;
            var peak = Math.Max(_peak, now);
            var dotnet = GC.GetGCMemoryInfo().TotalCommittedBytes;
            var line = $"memory: Crustcut {Mb(now)} (peak {Mb(peak)}) — .NET {Mb(dotnet)}, native {Mb(Math.Max(0, now - dotnet))}";
            if (_cleanups > 0)
                line += $"; {_cleanups} early cleanups gave back {Mb(_returned)} (gen {_generation})";
            if (_grewTotal > 0)
            {
                line += $"; grew {Mb(_grewTotal)}";
                if (jobs.Length > 0)
                    line += ", most during " + string.Join(", ",
                        jobs.OrderByDescending(j => j.Value).Take(4).Select(j => $"{j.Key} +{Mb(j.Value)}"));
            }
            _peak = now;
            _grewTotal = 0;
            _cleanups = 0;
            _returned = 0;
            return first ? null : line;
        }
    }

    private static string Mb(long bytes) => $"{bytes >> 20} MB";

    /// <summary>This process's private (committed) bytes — the "Commit size" in Task Manager.
    /// 0 where it can't be read, which turns everything here into a no-op.</summary>
    public static long PrivateBytes()
    {
        try
        {
            var c = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>() };
            return GetProcessMemoryInfo(GetCurrentProcess(), ref c, c.cb) ? (long)(ulong)c.PrivateUsage : 0;
        }
        catch { return 0; }   // not Windows
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr p, ref PROCESS_MEMORY_COUNTERS_EX c, uint cb);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
}
