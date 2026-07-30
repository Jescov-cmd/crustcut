using System.Diagnostics;
using System.Net.NetworkInformation;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Mac;

/// <summary>
/// macOS host metrics via the stock command-line tools every Mac ships with. No Mach
/// P/Invoke — shelling `sysctl`/`vm_stat`/`ps` is slower but debuggable and sandbox-safe.
/// GPU metrics are reported as 0: macOS has no public non-Metal API for GPU load.
/// UNTESTED ON REAL HARDWARE — built on Windows; every reader is fail-soft to 0.
/// </summary>
public sealed class MacHardwareClient : IHardwareClient
{
    private readonly long _totalRamBytes = ReadTotalRam();
    private long _lastNetRx, _lastNetTx;
    private DateTime _lastNetAt = DateTime.MinValue;

    public HardwareSnapshot Snapshot()
    {
        var (down, up) = SampleNetwork();
        return new HardwareSnapshot(
            CpuPercent: SampleCpuPercent(),
            RamUsedBytes: SampleRamUsed(),
            RamTotalBytes: _totalRamBytes,
            GpuPercent: 0,
            GpuTempC: 0,
            NetworkDownBps: down,
            NetworkUpBps: up);
    }

    public void Dispose() { }

    private static long ReadTotalRam()
    {
        var s = Run("/usr/sbin/sysctl", "-n hw.memsize");
        return long.TryParse(s.Trim(), out var v) ? v : 0;
    }

    /// <summary>Sum of per-process %CPU normalised by core count. `ps` reports a decaying
    /// average, so this tracks sustained load well and spikes a little late.</summary>
    private static double SampleCpuPercent()
    {
        var output = Run("/bin/ps", "-A -o %cpu=");
        double total = 0;
        foreach (var line in output.Split('\n'))
            if (double.TryParse(line.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var v))
                total += v;
        return Math.Clamp(total / Math.Max(1, Environment.ProcessorCount), 0, 100);
    }

    private long SampleRamUsed()
    {
        // vm_stat prints page counts; "Pages free" + "Pages inactive" approximate the
        // memory the OS would hand out on demand — everything else counts as used.
        var output = Run("/usr/bin/vm_stat", "");
        long pageSize = 16384, free = 0, inactive = 0;
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("Mach Virtual Memory Statistics", StringComparison.Ordinal))
            {
                var open = t.IndexOf("of ", StringComparison.Ordinal);
                if (open >= 0 && long.TryParse(new string(t[open..].Where(char.IsDigit).ToArray()), out var ps) && ps > 0)
                    pageSize = ps;
            }
            else if (t.StartsWith("Pages free:", StringComparison.Ordinal)) free = ParsePages(t);
            else if (t.StartsWith("Pages inactive:", StringComparison.Ordinal)) inactive = ParsePages(t);
        }
        var available = (free + inactive) * pageSize;
        return Math.Max(0, _totalRamBytes - available);
    }

    private static long ParsePages(string line)
    {
        var digits = new string(line.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var v) ? v : 0;
    }

    private (long Down, long Up) SampleNetwork()
    {
        try
        {
            long rx = 0, tx = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var stats = nic.GetIPStatistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }

            var now = DateTime.UtcNow;
            if (_lastNetAt == DateTime.MinValue)
            {
                (_lastNetRx, _lastNetTx, _lastNetAt) = (rx, tx, now);
                return (0, 0);
            }

            var secs = Math.Max(0.25, (now - _lastNetAt).TotalSeconds);
            var down = (long)((rx - _lastNetRx) / secs);
            var up = (long)((tx - _lastNetTx) / secs);
            (_lastNetRx, _lastNetTx, _lastNetAt) = (rx, tx, now);
            return (Math.Max(0, down), Math.Max(0, up));
        }
        catch
        {
            return (0, 0);   // per-interface statistics aren't guaranteed on every platform
        }
    }

    private static string Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return output;
        }
        catch
        {
            return "";
        }
    }
}
