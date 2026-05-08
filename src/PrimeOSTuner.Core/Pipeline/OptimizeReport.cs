namespace PrimeOSTuner.Core.Pipeline;

public sealed record OptimizeReport(
    int SuccessCount,
    int FailureCount,
    int SkippedDestructiveCount,
    IReadOnlyList<string> AppliedTweakIds,
    IReadOnlyList<(string TweakId, string Error)> Failures);
