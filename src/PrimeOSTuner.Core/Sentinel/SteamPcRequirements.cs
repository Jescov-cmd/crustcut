namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Parsed Steam PC requirements. Nullable fields = the parser could not
/// extract that value from the spec HTML. Treat null as "unknown" — never
/// fire a detection rule against an unknown value.
/// </summary>
public sealed record SteamPcRequirements(
    int? MinRamMb,
    int? RecRamMb,
    int? MinVramMb,
    int? RecVramMb);
