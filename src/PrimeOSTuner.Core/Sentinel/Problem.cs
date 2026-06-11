namespace PrimeOSTuner.Core.Sentinel;

public enum ProblemKind
{
    VramOverhead,
    RamPressure,
    CpuSaturated,
    CpuThrottled,
}

public sealed record Problem(ProblemKind Kind, string Detail, DateTime DetectedAt);
