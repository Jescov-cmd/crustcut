namespace PrimeOSTuner.Core.Bloatware;

/// <summary>
/// Curated matcher for known desktop (Win32) bloat. NameContains must appear in the
/// program's DisplayName (case-insensitive); PublisherContains, when set, must also
/// appear in Publisher — this prevents e.g. "HP" matching unrelated software.
/// </summary>
public sealed record DesktopBloatwareCatalogEntry(
    string Id,
    string DisplayName,
    string NameContains,
    string? PublisherContains,
    string Category,
    SafetyTier Tier,
    string? RiskNote);
