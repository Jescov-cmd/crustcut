namespace PrimeOSTuner.Core.Bloatware;

public enum BloatwareStatus
{
    Installed,           // present and running normally
    Disabled,            // present but startup/services disabled
    Uninstalled          // not installed for current user
}

/// <summary>
/// A bloatware catalog entry joined with the runtime install state of this machine.
/// </summary>
public sealed record BloatwareItem(
    BloatwareCatalogEntry Entry,
    BloatwareStatus Status,
    string? PackageFullName,    // e.g. "Microsoft.XboxGamingOverlay_5.823.1191.0_x64..."
    long? ApproximateSizeBytes  // best-effort; null if we couldn't measure
);
