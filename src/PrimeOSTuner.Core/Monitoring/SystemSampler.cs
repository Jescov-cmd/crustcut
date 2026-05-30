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

    private void Tick()
    {
        try
        {
            var s = _hardware.Snapshot();
            var ramPct = s.RamTotalBytes == 0 ? 0 : (double)s.RamUsedBytes / s.RamTotalBytes * 100.0;
            Sampled?.Invoke(this, new SystemSample(
                DateTime.UtcNow,
                s.CpuPercent, ramPct,
                s.RamUsedBytes, s.RamTotalBytes,
                s.GpuPercent, s.GpuTempC,
                s.NetworkDownBps, s.NetworkUpBps,
                s.VramUsedBytes, s.VramTotalBytes));
        }
        catch { /* one bad sample shouldn't kill the stream */ }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
