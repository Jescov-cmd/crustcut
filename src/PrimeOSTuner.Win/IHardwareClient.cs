namespace PrimeOSTuner.Win;

public sealed record HardwareSnapshot(
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    double GpuPercent,
    double GpuTempC,
    long NetworkDownBps,
    long NetworkUpBps);

public interface IHardwareClient : IDisposable
{
    HardwareSnapshot Snapshot();
}
