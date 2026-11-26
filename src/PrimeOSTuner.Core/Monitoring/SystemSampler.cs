using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Monitoring;

public sealed class SystemSampler : IDisposable
{
    private readonly IHardwareClient _hardware;
    private readonly System.Timers.Timer _timer;

    public event EventHandler<SystemSample>? Sampled;

    public SystemSampler(IHardwareClient hardware, int intervalMs = 1000)
    {
        _hardware = hardware;
        _timer = new System.Timers.Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private int _ticking;

    private void Tick()
    {
        // Timers.Timer fires on the thread pool whether or not the last tick finished. A
        // slow sample (counters stalling under memory pressure) used to overlap the next
        // one, blocking extra pool threads and racing the hardware client. Skip instead.
        if (Interlocked.Exchange(ref _ticking, 1) == 1) return;
        try
        {
            HardwareSnapshot s;
            using (PrimeOSTuner.Core.Diagnostics.MemoryWatch.Track("sensors")) s = _hardware.Snapshot();
            var ramPct = s.RamTotalBytes == 0 ? 0 : (double)s.RamUsedBytes / s.RamTotalBytes * 100.0;
            using var tracked = PrimeOSTuner.Core.Diagnostics.MemoryWatch.Track("sample listeners");
            Sampled?.Invoke(this, new SystemSample(
                DateTime.UtcNow,
                s.CpuPercent, ramPct,
                s.RamUsedBytes, s.RamTotalBytes,
                s.GpuPercent, s.GpuTempC,
                s.NetworkDownBps, s.NetworkUpBps,
                s.VramUsedBytes, s.VramTotalBytes, s.PageInputPerSec, s.MaxCoreDpcPercent));
        }
        catch { /* one bad sample shouldn't kill the stream */ }
        finally { Volatile.Write(ref _ticking, 0); }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
