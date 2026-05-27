namespace PrimeOSTuner.Core.Sentinel;

public interface IMetricsSampler
{
    /// <summary>
    /// Take one snapshot. Implementations may block briefly on perf counters.
    /// Any value the sampler cannot read should come back as -1 so the
    /// detection rules can treat it as "unknown" instead of "zero."
    /// </summary>
    Task<MetricsSnapshot> SampleAsync(int gamePid, CancellationToken ct = default);
}
