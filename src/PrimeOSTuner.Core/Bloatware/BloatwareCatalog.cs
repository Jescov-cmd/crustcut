using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Bloatware;

public static class BloatwareCatalog
{
    private sealed class Wrapper
    {
        [JsonPropertyName("items")]
        public List<BloatwareCatalogEntry> Items { get; set; } = new();
    }

    public static IReadOnlyList<BloatwareCatalogEntry> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Bloatware catalog not found at {path}");

        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var wrapper = JsonSerializer.Deserialize<Wrapper>(json, opts)
            ?? throw new InvalidOperationException("Bloatware catalog JSON is empty or invalid.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in wrapper.Items)
        {
            if (!seen.Add(entry.AppxName))
                throw new InvalidOperationException($"Bloatware catalog has duplicate appxName: {entry.AppxName}");
        }

        return wrapper.Items;
    }

    public static string DefaultPath()
    {
        var dir = AppContext.BaseDirectory;
        return Path.Combine(dir, "Bloatware", "catalog", "bloatware-list.json");
    }
}
