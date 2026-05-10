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
    private readonly PerformanceCounter[] _gpuCounters;
    private long _lastNetDown, _lastNetUp;
    private DateTime _lastNetSampleAt;

    public HardwareClient()
    {
        _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        // Prime the CPU counter — first read always returns 0.
        _ = _cpu.NextValue();

        _gpuCounters = TryBuildGpuCounters();
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

        return new HardwareSnapshot(cpu, ramUsed, ramTotal, gpu, 0.0, downBps, upBps);
    }

    private static double SafeRead(PerformanceCounter c)
    {
        try { return c.NextValue(); } catch { return 0; }
    }

    /// <summary>
    /// Build a counter per GPU engine instance. The category exists on Windows 10 1709+
    /// and isn't guaranteed on every system, so we tolerate failure and just report 0.
    /// </summary>
    private static PerformanceCounter[] TryBuildGpuCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return instances
                .Select(i => new PerformanceCounter("GPU Engine", "Utilization Percentage", i))
                .ToArray();
        }
        catch
        {
            return Array.Empty<PerformanceCounter>();
        }
    }

    private double SampleGpuPercent()
    {
        if (_gpuCounters.Length == 0) return 0;
        double total = 0;
        foreach (var c in _gpuCounters) total += SafeRead(c);
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
        foreach (var c in _gpuCounters) c.Dispose();
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
