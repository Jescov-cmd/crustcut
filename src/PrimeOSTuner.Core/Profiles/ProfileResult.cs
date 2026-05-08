namespace PrimeOSTuner.Core.Profiles;

public sealed record ProfileTweakOutcome(
    string TweakId,
    bool Succeeded,
    string? UndoData,
    string? Error);

public sealed record ProfileResult(
    string ProfileId,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ProfileTweakOutcome> Outcomes)
{
    public bool AllSucceeded => FailureCount == 0;
}
