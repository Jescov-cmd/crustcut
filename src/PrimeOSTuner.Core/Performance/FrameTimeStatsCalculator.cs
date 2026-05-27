namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Pure stats computer over raw frame-time samples in milliseconds.
/// Negative and zero samples are filtered out — they represent either the
/// first-row sentinel PresentMon writes or malformed rows.
/// </summary>
public static class FrameTimeStatsCalculator
{
    public static FrameSessionStats Compute(IReadOnlyList<double> samples)
    {
        var valid = samples.Where(s => s > 0).ToArray();
        if (valid.Length == 0)
            return new FrameSessionStats(0, 0, 0, 0, 0, 0, 0, 0);

        Array.Sort(valid);

        var avgMs = valid.Average();
        var avgFps = 1000.0 / avgMs;

        var p50  = Percentile(valid, 0.50);
        var p99  = Percentile(valid, 0.99);
        var p999 = Percentile(valid, 0.999);
        var max  = valid[^1];

        return new FrameSessionStats(
            AvgFps: avgFps,
            OnePctLowFps: 1000.0 / p99,
            ZeroPointOnePctLowFps: 1000.0 / p999,
            P50FrameTimeMs: p50,
            P99FrameTimeMs: p99,
            P999FrameTimeMs: p999,
            MaxFrameTimeMs: max,
            SampleCount: valid.Length);
    }

    // Percentile on a pre-sorted ascending array. Uses floor(pct * N) so that
    // P99 of 1000 samples lands at index 990 — i.e. inside the slowest-1% tail —
    // which matches the conventional "1% low" interpretation in gaming benchmarks.
    private static double Percentile(double[] sorted, double pct)
    {
        if (sorted.Length == 0) return 0;
        var rank = (int)(pct * sorted.Length);
        if (rank < 0) rank = 0;
        if (rank >= sorted.Length) rank = sorted.Length - 1;
        return sorted[rank];
    }
}
