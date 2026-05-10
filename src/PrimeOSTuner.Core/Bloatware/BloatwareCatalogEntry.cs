namespace PrimeOSTuner.Core.Bloatware;

/// <summary>
/// One row from bloatware-list.json — describes a known bloatware AppX package
/// regardless of whether it's actually installed on this machine.
/// </summary>
public sealed record BloatwareCatalogEntry(
    string AppxName,         // exact AppX package name, e.g. "Microsoft.XboxGamingOverlay"
    string DisplayName,      // friendly name, e.g. "Xbox Game Bar"
    string Category,         // "gaming" | "preinstalled" | "microsoft-extra" | "system" | "oem"
    SafetyTier Tier,
    string? RiskNote         // shown in warning dialog for Risky tier; tooltip for Blocked tier
);
