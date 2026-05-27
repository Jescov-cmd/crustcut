using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Win11-friendly metrics sampler. Reads system CPU via PerformanceCounter,
/// system RAM via GlobalMemoryStatusEx, and dedicated GPU memory via the
/// <c>\GPU Adapter Memory(*)\Dedicated Usage</c> counter (cross-vendor on
/// modern Windows).
/// Not thread-safe — call <see cref="SampleAsync"/> serially. The Sentinel watcher
/// loop satisfies this by design (it awaits each sample before scheduling the next).
/// </summary>
public sealed class GpuPerfCounterMetricsSampler : IMetricsSampler, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private bool _cpuPrimed;

    public GpuPerfCounterMetricsSampler()
    {
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
        catch { _cpuCounter = null; }
    }

    public async Task<MetricsSnapshot> SampleAsync(int gamePid, CancellationToken ct = default)
    {
        double cpu;
        if (_cpuCounter is null)
        {
            cpu = -1;
        }
        else
        {
            // PerformanceCounter's first read is always 0 — prime it once.
            if (!_cpuPrimed)
            {
                try { _cpuCounter.NextValue(); }
                catch { /* sampler will surface -1 below */ }
                await Task.Delay(120, ct);
                _cpuPrimed = true;
            }
            try { cpu = _cpuCounter.NextValue(); }
            catch { cpu = -1; }
        }

        var (ramUsed, ramTotal) = ReadSystemRam();
        var (vramUsed, vramTotal) = ReadDedicatedGpuMemory();

        var now = DateTime.UtcNow;
        return new MetricsSnapshot(now, gamePid, cpu, ramUsed, ramTotal, vramUsed, vramTotal);
    }

    private static (long Used, long Total) ReadSystemRam()
    {
        var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref s)) return (-1, -1);
        return ((long)(s.ullTotalPhys - s.ullAvailPhys), (long)s.ullTotalPhys);
    }

    private static (long Used, long Total) ReadDedicatedGpuMemory()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var instances = category.GetInstanceNames();
            if (instances.Length == 0) return (-1, -1);

            long used = 0;
            long total = 0;
            foreach (var inst in instances)
            {
                using var usage = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", inst, readOnly: true);
                used += (long)usage.NextValue();

                // "Dedicated Limit" is the per-adapter capacity. Newer Windows builds
                // expose it; older builds only have Usage.
                try
                {
                    using var limit = new PerformanceCounter("GPU Adapter Memory", "Dedicated Limit", inst, readOnly: true);
                    total += (long)limit.NextValue();
                }
                catch { /* leave total at 0 — caller treats <=0 as unknown */ }
            }
            return (used, total > 0 ? total : -1);
        }
        catch
        {
            return (-1, -1);
        }
    }

    public void Dispose() => _cpuCounter?.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
