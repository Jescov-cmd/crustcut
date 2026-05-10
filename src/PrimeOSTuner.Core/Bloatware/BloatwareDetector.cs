namespace PrimeOSTuner.Core.Bloatware;

public sealed class BloatwareDetector
{
    private readonly IAppxClient _appx;
    private readonly IReadOnlyList<BloatwareCatalogEntry> _catalog;

    public BloatwareDetector(IAppxClient appx, IReadOnlyList<BloatwareCatalogEntry> catalog)
    {
        _appx = appx;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<BloatwareItem>> DetectAsync(CancellationToken ct = default)
    {
        var installed = await _appx.ListInstalledAsync(ct);
        var lookup = installed
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var found = new List<BloatwareItem>();
        foreach (var entry in _catalog)
        {
            if (!lookup.TryGetValue(entry.AppxName, out var inst)) continue;
            // Approximating size by InstallLocation folder is best-effort and slow on first call.
            // Defer it to a separate method to keep DetectAsync responsive.
            found.Add(new BloatwareItem(entry, BloatwareStatus.Installed, inst.PackageFullName, null));
        }

        // Sort: Safe → Risky → Blocked, then alphabetical within each tier.
        return found
            .OrderBy(i => (int)i.Entry.Tier)
            .ThenBy(i => i.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
