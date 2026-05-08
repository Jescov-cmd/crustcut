namespace PrimeOSTuner.Core.Profiles;

public sealed record ModeProfile(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<string> TweakIds);
