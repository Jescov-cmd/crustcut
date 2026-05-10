namespace PrimeOSTuner.Core.Bloatware;

public sealed class BloatwareUninstallService
{
    private readonly IAppxClient _appx;

    public BloatwareUninstallService(IAppxClient appx)
    {
        _appx = appx;
    }

    /// <summary>
    /// Uninstalls the package for the current user AND removes the provisioned package
    /// so it doesn't reappear for new accounts. Throws on Blocked tier.
    /// </summary>
    public async Task UninstallAsync(BloatwareItem item, CancellationToken ct = default)
    {
        if (item.Entry.Tier == SafetyTier.Blocked)
            throw new InvalidOperationException(
                $"'{item.Entry.DisplayName}' is in the Blocked tier and cannot be uninstalled.");

        if (string.IsNullOrEmpty(item.PackageFullName))
            throw new InvalidOperationException("No PackageFullName for the item — was it actually detected?");

        await _appx.RemoveAsync(item.PackageFullName, ct);

        // Best-effort: removing the provisioned package may fail on some systems.
        // Don't let that fail the whole uninstall.
        try
        {
            await _appx.RemoveProvisionedAsync(item.Entry.AppxName, ct);
        }
        catch
        {
            // Swallow — primary uninstall already succeeded.
        }
    }
}
