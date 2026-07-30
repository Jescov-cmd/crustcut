using System.Diagnostics;
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Diagnosis;

/// <summary>
/// Real OS probes for the Diagnosis scan: perf counters, process sampling, DriveInfo,
/// powercfg, and the display-class registry. Lives in Core beside the Sentinel sampler
/// (same PerformanceCounter dependency); every probe returns null on failure so the
/// rules stay silent instead of guessing.
/// </summary>
public sealed class DiagnosisProbes : IDiagnosisProbes
{
    /// <summary>Spin half the cores for ~2.5 s and average "% Processor Performance" — well under 100% under load means power/thermal limiting.</summary>
    public async Task<double?> SampleCpuPerformanceUnderLoadAsync(CancellationToken ct)
    {
        try
        {
            using var counter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
            counter.NextValue();   // first read is always 0

            using var stop = new CancellationTokenSource();
            var burners = Enumerable.Range(0, Math.Max(1, Environment.ProcessorCount / 2))
                .Select(_ => Task.Run(() => { var x = 1.0; while (!stop.IsCancellationRequested) x = Math.Sqrt(x + 1.0001); }))
                .ToArray();

            var samples = new List<float>();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(500, ct);
                    samples.Add(counter.NextValue());
                }
            }
            finally
            {
                stop.Cancel();
                await Task.WhenAll(burners);
            }

            return samples.Count > 0 ? samples.Average() : null;
        }
        catch { return null; }   // counter missing / locale issues → silent
    }

    /// <summary>Two-point CPU sampling per process (~1.2 s apart) so CpuPercent is a real rate, not a cumulative total.</summary>
    public async Task<IReadOnlyList<ProcSample>> SampleTopProcessesAsync(CancellationToken ct)
    {
        try
        {
            static Dictionary<int, (string Name, TimeSpan Cpu, long Ws)> Snap() =>
                Process.GetProcesses()
                    .Where(p => p.SessionId != 0)              // skip services/system session
                    .Select(p =>
                    {
                        try { return (p.Id, Name: p.ProcessName, Cpu: p.TotalProcessorTime, Ws: p.WorkingSet64); }
                        catch { return (p.Id, Name: "", Cpu: TimeSpan.Zero, Ws: 0L); }
                    })
                    .Where(t => t.Name.Length > 0)
                    .GroupBy(t => t.Id)
                    .ToDictionary(g => g.Key, g => (g.First().Name, g.First().Cpu, g.First().Ws));

            var a = Snap();
            await Task.Delay(1200, ct);
            var b = Snap();

            var wall = 1.2 * Environment.ProcessorCount;
            var own = Process.GetCurrentProcess().ProcessName;
            return b.Where(kv => a.ContainsKey(kv.Key) && kv.Value.Name != own)
                .Select(kv => new ProcSample(
                    kv.Value.Name,
                    Math.Max(0, (kv.Value.Cpu - a[kv.Key].Cpu).TotalSeconds) / wall * 100.0,
                    kv.Value.Ws))
                .OrderByDescending(p => p.CpuPercent)
                .Take(12)
                .ToList();
        }
        catch { return Array.Empty<ProcSample>(); }
    }

    public double? RamUsedPercent()
    {
        try
        {
            var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (total <= 0) return null;
            using var avail = new PerformanceCounter("Memory", "Available Bytes", readOnly: true);
            var free = (double)avail.NextValue();
            if (free <= 0) return null;
            return 100.0 * (total - free) / total;
        }
        catch { return null; }
    }

    public (long Free, long Total)? SystemDriveSpace()
    {
        try
        {
            var sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
            var d = new DriveInfo(sys);
            return (d.AvailableFreeSpace, d.TotalSize);
        }
        catch { return null; }
    }

    public string? ActivePowerPlanName()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            var output = p!.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            var open = output.IndexOf('(');
            var close = output.IndexOf(')');
            return open >= 0 && close > open ? output[(open + 1)..close].Trim() : null;
        }
        catch { return null; }
    }

    /// <summary>Newest DriverDate across display-adapter class subkeys (format "M-d-yyyy").</summary>
    public DateTime? NewestGpuDriverDateUtc()
    {
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls is null) return null;
            DateTime? newest = null;
            foreach (var sub in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
            {
                try
                {
                    using var k = cls.OpenSubKey(sub);
                    if (k?.GetValue("DriverDate") is string s
                        && DateTime.TryParseExact(s, "M-d-yyyy", null,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var dt)
                        && (newest is null || dt > newest)) newest = dt;
                }
                catch { }
            }
            return newest;
        }
        catch { return null; }
    }
}
