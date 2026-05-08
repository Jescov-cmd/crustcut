namespace PrimeOSTuner.Core.Profiles;

public sealed record ActiveTweaksRecord(
    string GameId,
    string ProfileId,
    DateTime AppliedAtUtc,
    IReadOnlyList<ProfileTweakOutcome> Outcomes);
