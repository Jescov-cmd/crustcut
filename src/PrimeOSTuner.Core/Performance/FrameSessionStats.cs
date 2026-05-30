namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Summary statistics computed from a session's raw frame-time samples.
/// All frame-time values are in milliseconds; FPS values are derived.
/// </summary>
public sealed record FrameSessionStats(
    double AvgFps,
    double OnePctLowFps,       // = 1000 / P99FrameTimeMs
    double ZeroPointOnePctLowFps, // = 1000 / P999FrameTimeMs
    double P50FrameTimeMs,
    double P99FrameTimeMs,
    double P999FrameTimeMs,
    double MaxFrameTimeMs,
    int SampleCount,
    double MaxFps = 0);        // = 1000 / fastest frame time ("highest")
