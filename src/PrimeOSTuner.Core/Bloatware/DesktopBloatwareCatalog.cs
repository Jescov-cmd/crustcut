using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Bloatware;

/// <summary>Loads the curated desktop (Win32) bloatware matchers. Mirrors BloatwareCatalog.</summary>
public static class DesktopBloatwareCatalog
{
    private sealed class Wrapper
    {
        [JsonPropertyName("items")]
        public List<DesktopBloatwareCatalogEntry> Items { get; set; } = new();
    }

    public static IReadOnlyList<DesktopBloatwareCatalogEntry> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Desktop bloatware catalog not found at {path}");

        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var wrapper = JsonSerializer.Deserialize<Wrapper>(json, opts)
            ?? throw new InvalidOperationException("Desktop bloatware catalog JSON is empty or invalid.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in wrapper.Items)
        {
            if (!seen.Add(entry.Id))
                throw new InvalidOperationException($"Desktop bloatware catalog has duplicate id: {entry.Id}");
        }

        return wrapper.Items;
    }

    public static string DefaultPath()
        => Path.Combine(AppContext.BaseDirectory, "Bloatware", "catalog", "desktop-bloatware-list.json");
}
