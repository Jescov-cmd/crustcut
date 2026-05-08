namespace PrimeOSTuner.Core.Monitoring;

public sealed record BoostScoreInputs(
    long JunkBytes,
    bool HighPerformancePower,
    bool VisualEffectsOptimized,
    bool MouseAccelDisabled,
    bool TelemetryDisabled,
    int BloatwareCount);
