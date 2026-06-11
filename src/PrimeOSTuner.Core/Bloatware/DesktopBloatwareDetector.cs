namespace PrimeOSTuner.Core.Bloatware;

public sealed record DesktopBloatHit(DesktopBloatwareCatalogEntry Entry, DesktopProgram Program);

public static class DesktopBloatwareDetector
{
    /// <summary>Pure matcher: first catalog entry to match a program claims it.</summary>
    public static IReadOnlyList<DesktopBloatHit> Match(
        IEnumerable<DesktopProgram> installed,
        IEnumerable<DesktopBloatwareCatalogEntry> catalog)
    {
        var entries = catalog.ToList();
        var hits = new List<DesktopBloatHit>();
        foreach (var prog in installed)
        {
            if (string.IsNullOrWhiteSpace(prog.DisplayName)) continue;
            var entry = entries.FirstOrDefault(e =>
                prog.DisplayName.Contains(e.NameContains, StringComparison.OrdinalIgnoreCase)
                && (e.PublisherContains is null
                    || (prog.Publisher?.Contains(e.PublisherContains, StringComparison.OrdinalIgnoreCase) ?? false)));
            if (entry is not null) hits.Add(new DesktopBloatHit(entry, prog));
        }
        return hits
            .OrderBy(h => (int)h.Entry.Tier)
            .ThenBy(h => h.Program.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
