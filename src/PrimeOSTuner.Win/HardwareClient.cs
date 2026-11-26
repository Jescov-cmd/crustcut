using System;
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

/// <summary>
/// Pulls live system stats from Windows' built-in counters and APIs.
///
/// Why no kernel driver: an earlier version used LibreHardwareMonitor, which extracts
/// the WinRing0 driver to read CPU MSRs. Microsoft's vulnerable-driver list flags
/// WinRing0 — Defender removes the .sys file on sight. Performance counters + the
/// Win32 memory API cover everything we display, with no driver and no Defender drama.
///
/// Everything counter-based goes through ONE PDH query collected once per sample. The
/// previous version used System.Diagnostics.PerformanceCounter: every second it
/// re-listed every "GPU Engine" instance (the whole category's data block, a large-object
/// allocation), kept one counter per GPU-using process — each of which re-read that same
/// block — and built two brand-new VRAM counters per adapter. PDH wildcard counters do
/// the instance tracking natively: a game launched after startup appears on the next
/// collect, a closed one disappears, and nothing is allocated per sample.
///
/// Tradeoff: GPU temperature isn't exposed by Windows, so it's reported as 0.
/// The dashboard never displayed it anyway.
/// </summary>
public sealed class HardwareClient : IHardwareClient, IGpuUsageByProcess
{
    private readonly object _gate = new();
    private IntPtr _query;
    private IntPtr _cpu, _gpu3d, _vramUsed, _vramLimit, _pageIn, _coreDpc;
    private long _lastNetDown, _lastNetUp;
    private DateTime _lastNetSampleAt;
    private IntPtr _buffer;
    private int _bufferSize;
    private Dictionary<int, double> _gpuByPid = new();

    /// <summary>Per-process 3D use from the latest sample. The GPU Engine instances are
    /// already per process ("pid_1234_luid_…_engtype_3D"), so this costs nothing extra.</summary>
    public IReadOnlyDictionary<int, double> GpuPercentByPid => _gpuByPid;

    public HardwareClient()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) { _query = IntPtr.Zero; return; }

        // English names so this works on non-English Windows installs.
        _cpu       = Add(@"\Processor(_Total)\% Processor Time");
        _gpu3d     = Add(@"\GPU Engine(*engtype_3D)\Utilization Percentage");
        _vramUsed  = Add(@"\GPU Adapter Memory(*)\Dedicated Usage");
        _vramLimit = Add(@"\GPU Adapter Memory(*)\Dedicated Limit");
        _pageIn    = Add(@"\Memory\Pages Input/sec");
        // Per core, not _Total: a driver stall pins ONE core (the network card usually
        // lands on core 0), and averaging it across 16 threads hides it completely.
        _coreDpc   = Add(@"\Processor(*)\% DPC Time");

        // Rate counters (CPU, GPU utilisation, network) need two collects before the
        // first real value — prime now so the first sample isn't all zeros.
        PdhCollectQueryData(_query);

        _lastNetSampleAt = DateTime.UtcNow;
        ReadNetworkTotals(out _lastNetDown, out _lastNetUp);
    }

    private IntPtr Add(string path) =>
        PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;

    public HardwareSnapshot Snapshot()
    {
        var mem = MEMORYSTATUSEX.Create();
        long ramTotal = 0, ramUsed = 0;
        if (GlobalMemoryStatusEx(ref mem))
        {
            ramTotal = (long)mem.ullTotalPhys;
            ramUsed  = (long)(mem.ullTotalPhys - mem.ullAvailPhys);
        }

        lock (_gate)
        {
            // Network stays on the interface statistics: the PDH "Network Interface"
            // counters include virtual adapters (Hyper-V, WSL), which double-count traffic.
            ReadNetworkTotals(out var nowDown, out var nowUp);
            var now = DateTime.UtcNow;
            var dt = (now - _lastNetSampleAt).TotalSeconds;
            long down = 0, up = 0;
            if (dt > 0.05)
            {
                down = (long)Math.Max(0, (nowDown - _lastNetDown) / dt);
                up   = (long)Math.Max(0, (nowUp   - _lastNetUp)   / dt);
            }
            _lastNetDown = nowDown;
            _lastNetUp = nowUp;
            _lastNetSampleAt = now;

            if (_query == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
                return new HardwareSnapshot(0, ramUsed, ramTotal, 0, 0, down, up, 0, 0);

            var cpu = Single(_cpu);
            var byPid = new Dictionary<int, double>();
            var gpu = Math.Min(100, Reduce(_gpu3d, max: false, byPid));
            _gpuByPid = byPid;   // swapped whole: readers on other threads never see it half-built
            var vramUsed = (long)Reduce(_vramUsed, max: false);
            var vramTotal = (long)Reduce(_vramLimit, max: false);
            var pageIn = Single(_pageIn);
            var coreDpc = Reduce(_coreDpc, max: true);
            return new HardwareSnapshot(Math.Clamp(cpu, 0, 100), ramUsed, ramTotal, gpu, 0.0,
                down, up, vramUsed, vramTotal, pageIn, coreDpc);
        }
    }

    private static void ReadNetworkTotals(out long downBytes, out long upBytes)
    {
        downBytes = 0;
        upBytes = 0;
        try
        {
            foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var stats = iface.GetIPv4Statistics();
                downBytes += stats.BytesReceived;
                upBytes   += stats.BytesSent;
            }
        }
        catch { /* counters can briefly throw during interface state changes */ }
    }

    private static double Single(IntPtr counter)
    {
        if (counter == IntPtr.Zero) return 0;
        return PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE, out _, out var v) == 0
               && v.CStatus <= PDH_CSTATUS_NEW_DATA
            ? v.DoubleValue
            : 0;
    }

    /// <summary>"pid_1234_luid_0x…_engtype_3D" → 1234.</summary>
    internal static int? PidOf(string? instance)
    {
        if (instance is null || !instance.StartsWith("pid_", StringComparison.Ordinal)) return null;
        var end = instance.IndexOf('_', 4);
        return end > 4 && int.TryParse(instance.AsSpan(4, end - 4), out var pid) ? pid : null;
    }

    /// <summary>Sum (or max) of every instance of a wildcard counter, never counting the
    /// "_Total" instance. Instances with no valid reading yet (a process that started using
    /// the GPU since the last collect) are skipped rather than counted as zero-with-an-error.</summary>
    private double Reduce(IntPtr counter, bool max, Dictionary<int, double>? perPid = null)
    {
        if (counter == IntPtr.Zero) return 0;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var size = _bufferSize;
            var status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100,
                ref size, out var count, _buffer);
            if (status == PDH_MORE_DATA)
            {
                // Grow the reusable buffer (with headroom) and retry once.
                if (_buffer != IntPtr.Zero) Marshal.FreeHGlobal(_buffer);
                _bufferSize = Math.Max(size, 4096) * 2;
                _buffer = Marshal.AllocHGlobal(_bufferSize);
                continue;
            }
            if (status != 0) return 0;

            double result = 0;
            var itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(_buffer + i * itemSize);
                if (item.Value.CStatus > PDH_CSTATUS_NEW_DATA) continue;
                // Names are only read where they matter: the per-core max must skip the
                // "_Total" instance, and the per-process GPU map needs each instance's pid.
                if (max || perPid is not null)
                {
                    var name = Marshal.PtrToStringUni(item.Name);
                    if (name == "_Total") continue;
                    if (perPid is not null && PidOf(name) is int pid)
                        perPid[pid] = perPid.GetValueOrDefault(pid) + item.Value.DoubleValue;
                }
                result = max ? Math.Max(result, item.Value.DoubleValue) : result + item.Value.DoubleValue;
            }
            return result;
        }
        return 0;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; }
            if (_buffer != IntPtr.Zero) { Marshal.FreeHGlobal(_buffer); _buffer = IntPtr.Zero; }
        }
    }

    // ── PDH interop ───────────────────────────────────────────────────────────────────
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_FMT_NOCAP100 = 0x00008000;
    private const uint PDH_MORE_DATA = 0x800007D2;
    private const uint PDH_CSTATUS_NEW_DATA = 0x00000001;   // VALID_DATA is 0

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr Name;
        public PDH_FMT_COUNTERVALUE Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhOpenQueryW")]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type,
        out PDH_FMT_COUNTERVALUE value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref int bufferSize,
        out int itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

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
