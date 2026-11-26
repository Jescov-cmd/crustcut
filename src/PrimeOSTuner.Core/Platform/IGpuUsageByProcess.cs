namespace PrimeOSTuner.Win;

/// <summary>3D graphics-engine use per process from the latest sample, in percent. How
/// Crustcut tells a game it has never heard of from everything else on screen.</summary>
public interface IGpuUsageByProcess
{
    IReadOnlyDictionary<int, double> GpuPercentByPid { get; }
}
