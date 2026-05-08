namespace PrimeOSTuner.Core.Monitoring;

public static class BoostScoreCalculator
{
    public static int Compute(BoostScoreInputs i)
    {
        // Start at 100 and subtract penalties.
        var score = 100.0;
        score -= Math.Min(25, i.JunkBytes / (double)(1L << 30) * 5); // up to -25 for >5GB junk
        if (!i.HighPerformancePower)      score -= 10;
        if (!i.VisualEffectsOptimized)    score -= 5;
        if (!i.MouseAccelDisabled)        score -= 8;
        if (!i.TelemetryDisabled)         score -= 12;
        score -= Math.Min(20, i.BloatwareCount * 1.5); // up to -20 for bloat

        return (int)Math.Clamp(score, 0, 100);
    }
}
