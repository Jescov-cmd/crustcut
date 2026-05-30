namespace PrimeOSTuner.Core.Monitoring;

public sealed record SystemSample(
    DateTime TakenAtUtc,
    double CpuPercent,
    double RamPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    double GpuPercent,
    double GpuTempC,
    long NetworkDownBps,
    long NetworkUpBps,
    long VramUsedBytes = 0,
    long VramTotalBytes = 0);
