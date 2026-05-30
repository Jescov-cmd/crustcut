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
    long VramTotalBytes = 0);

public interface IHardwareClient : IDisposable
{
    HardwareSnapshot Snapshot();
}
