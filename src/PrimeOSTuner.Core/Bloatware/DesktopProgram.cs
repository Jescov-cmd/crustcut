namespace PrimeOSTuner.Core.Bloatware;

/// <summary>One row from the Windows installed-programs registry (Uninstall keys).</summary>
public sealed record DesktopProgram(
    string DisplayName,
    string? Publisher,
    string UninstallString,
    string? QuietUninstallString,
    string? InstallLocation);
