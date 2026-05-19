namespace PrimeOSTuner.Core.Education;

/// <summary>
/// Loads "Optimization 101" guides from a directory of markdown files
/// (one guide per <c>.md</c> file, parsed by <see cref="GuideParser"/>).
/// </summary>
public static class GuideCatalog
{
    /// <summary>The folder guide markdown files ship in, next to the app binaries.</summary>
    public static string DefaultDirectory()
        => Path.Combine(AppContext.BaseDirectory, "Education", "guides");

    public static IReadOnlyList<Guide> LoadFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Guide directory not found: {directory}");

        var guides = new List<Guide>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(directory, "*.md").OrderBy(f => f, StringComparer.Ordinal))
        {
            Guide guide;
            try
            {
                guide = GuideParser.Parse(File.ReadAllText(file));
            }
            catch (FormatException ex)
            {
                throw new FormatException(
                    $"Guide file '{Path.GetFileName(file)}' is malformed: {ex.Message}", ex);
            }

            if (!seen.Add(guide.Id))
                throw new InvalidOperationException(
                    $"Duplicate guide id '{guide.Id}' (found again in '{Path.GetFileName(file)}').");

            guides.Add(guide);
        }

        return guides;
    }
}
