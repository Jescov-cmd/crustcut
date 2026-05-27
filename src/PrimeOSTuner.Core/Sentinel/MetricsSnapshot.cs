namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// A single point-in-time snapshot of system + game-process resource use.
/// Negative values (-1) mean "the sampler could not read this metric" — rules
/// treat that as unknown and stay silent.
/// </summary>
public sealed record MetricsSnapshot(
    DateTime At,
    int GamePid,
    double SystemCpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long VramUsedBytes,
    long VramTotalBytes);
