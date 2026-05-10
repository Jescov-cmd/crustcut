namespace PrimeOSTuner.Core.Bloatware;

public sealed record InstalledAppx(
    string Name,                     // package name, e.g. "Microsoft.XboxGamingOverlay"
    string PackageFullName,          // full identity including version
    string? InstallLocation          // disk path; null on locked-down systems
);

public interface IAppxClient
{
    /// <summary>Enumerate AppX packages installed for the current user.</summary>
    Task<IReadOnlyList<InstalledAppx>> ListInstalledAsync(CancellationToken ct = default);

    /// <summary>Uninstall an AppX package for the current user.</summary>
    Task RemoveAsync(string packageFullName, CancellationToken ct = default);

    /// <summary>Remove the provisioned package so it doesn't reappear for new users on this machine.</summary>
    Task RemoveProvisionedAsync(string packageName, CancellationToken ct = default);
}
