namespace PrimeOSTuner.Win;

public sealed record HardwareSnapshot(
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    double GpuPercent,
    double GpuTempC,
    long NetworkDownBps,
    long NetworkUpBps,
    long VramUsedBytes = 0,
    long VramTotalBytes = 0,
    double PageInputPerSec = 0,    // hard-fault reads: memory being pulled back off disk
    double MaxCoreDpcPercent = 0); // busiest core's time spent in drivers (DPCs)

public interface IHardwareClient : IDisposable
{
    HardwareSnapshot Snapshot();
}
