using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

/// <summary>
/// Pulls live system stats from Windows' built-in counters and APIs.
///
/// Why no kernel driver: an earlier version used LibreHardwareMonitor, which extracts
/// the WinRing0 driver to read CPU MSRs. Microsoft's vulnerable-driver list flags
/// WinRing0 — Defender removes the .sys file on sight. PerformanceCounter + the
/// Win32 memory API cover everything we display, with no driver and no Defender drama.
///
/// Tradeoff: GPU temperature isn't exposed by Windows, so it's reported as 0.
/// The dashboard never displayed it anyway.
/// </summary>
public sealed class HardwareClient : IHardwareClient
{
    private readonly PerformanceCounter _cpu;
    // GPU Engine "Utilization Percentage" instances are PER PROCESS and appear only once a
    // process starts using the GPU. Building them once at startup missed games launched
    // later (hence "Fortnite shows 2%"). Keep a live dictionary keyed by instance name and
    // refresh it each sample — adding new instances, dropping gone ones.
    private readonly Dictionary<string, PerformanceCounter> _gpuByInstance = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _vramInstances;
    private long _lastNetDown, _lastNetUp;
    private DateTime _lastNetSampleAt;

    public HardwareClient()
    {
        _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        // Prime the CPU counter — first read always returns 0.
        _ = _cpu.NextValue();

        _vramInstances = TryGetVramInstances();
        _lastNetSampleAt = DateTime.UtcNow;
        ReadNetworkTotals(out _lastNetDown, out _lastNetUp);
    }

    public HardwareSnapshot Snapshot()
    {
        var cpu = SafeRead(_cpu);

        var mem = MEMORYSTATUSEX.Create();
        long ramTotal = 0, ramUsed = 0;
        if (GlobalMemoryStatusEx(ref mem))
        {
            ramTotal = (long)mem.ullTotalPhys;
            ramUsed  = (long)(mem.ullTotalPhys - mem.ullAvailPhys);
        }

        var gpu = SampleGpuPercent();
        var (vramUsed, vramTotal) = SampleVram();

        ReadNetworkTotals(out var nowDown, out var nowUp);
        var now = DateTime.UtcNow;
        var dt = (now - _lastNetSampleAt).TotalSeconds;
        long downBps = 0, upBps = 0;
        if (dt > 0.05)
        {
            downBps = (long)Math.Max(0, (nowDown - _lastNetDown) / dt);
            upBps   = (long)Math.Max(0, (nowUp   - _lastNetUp)   / dt);
        }
        _lastNetDown = nowDown;
        _lastNetUp   = nowUp;
        _lastNetSampleAt = now;

        return new HardwareSnapshot(cpu, ramUsed, ramTotal, gpu, 0.0, downBps, upBps, vramUsed, vramTotal);
    }

    private static string[] TryGetVramInstances()
    {
        try
        {
            return new PerformanceCounterCategory("GPU Adapter Memory").GetInstanceNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Dedicated VRAM used / total across adapters, via the cross-vendor
    /// "GPU Adapter Memory" counters. Returns (0,0) if unavailable.</summary>
    private (long Used, long Total) SampleVram()
    {
        if (_vramInstances.Length == 0) return (0, 0);
        long used = 0, total = 0;
        foreach (var inst in _vramInstances)
        {
            try
            {
                using var usage = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", inst, readOnly: true);
                used += (long)usage.NextValue();
            }
            catch { }
            try
            {
                using var limit = new PerformanceCounter("GPU Adapter Memory", "Dedicated Limit", inst, readOnly: true);
                total += (long)limit.NextValue();
            }
            catch { }
        }
        return (used, total);
    }

    private static double SafeRead(PerformanceCounter c)
    {
        try { return c.NextValue(); } catch { return 0; }
    }

    /// <summary>
    /// Total 3D-engine GPU utilization. Re-enumerates the per-process "engtype_3D"
    /// instances each call so a game launched after startup is included, while keeping
    /// each counter object alive across samples (the utilization counter needs a prior
    /// reading to produce a non-zero value). Caps at 100%.
    /// </summary>
    private double SampleGpuPercent()
    {
        string[] current;
        try
        {
            current = new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return 0;   // category not present on this Windows build
        }

        var live = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);

        // Drop counters for instances that no longer exist (process closed).
        foreach (var gone in _gpuByInstance.Keys.Where(k => !live.Contains(k)).ToList())
        {
            try { _gpuByInstance[gone].Dispose(); } catch { }
            _gpuByInstance.Remove(gone);
        }

        double total = 0;
        foreach (var inst in current)
        {
            if (!_gpuByInstance.TryGetValue(inst, out var counter))
            {
                try
                {
                    counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, readOnly: true);
                    counter.NextValue();   // prime: first read seeds the next real value
                    _gpuByInstance[inst] = counter;
                }
                catch { continue; }
            }
            total += SafeRead(counter);
        }
        return Math.Min(100, total);
    }

    private static void ReadNetworkTotals(out long downBytes, out long upBytes)
    {
        downBytes = 0;
        upBytes = 0;
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var stats = iface.GetIPv4Statistics();
                downBytes += stats.BytesReceived;
                upBytes   += stats.BytesSent;
            }
        }
        catch { /* counters can briefly throw during interface state changes */ }
    }

    public void Dispose()
    {
        _cpu.Dispose();
        foreach (var c in _gpuByInstance.Values) { try { c.Dispose(); } catch { } }
        _gpuByInstance.Clear();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

        public static MEMORYSTATUSEX Create() => new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
