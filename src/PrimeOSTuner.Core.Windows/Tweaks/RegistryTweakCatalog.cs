using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Tweaks;

public static class RegistryTweakCatalog
{
    private sealed class Wrapper
    {
        [JsonPropertyName("tweaks")]
        public List<RegistryTweakDefinition> Tweaks { get; set; } = new();
    }

    public static IReadOnlyList<RegistryTweakDefinition> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Tweak catalog not found at {path}");

        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var wrapper = JsonSerializer.Deserialize<Wrapper>(json, opts)
            ?? throw new InvalidOperationException("Tweak catalog JSON is empty or invalid.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in wrapper.Tweaks)
        {
            if (!seen.Add(t.Id))
                throw new InvalidOperationException($"Tweak catalog has duplicate id: {t.Id}");
        }

        return wrapper.Tweaks;
    }

    public static string DefaultPath()
    {
        var dir = AppContext.BaseDirectory;
        return Path.Combine(dir, "Tweaks", "catalog", "tweaks.json");
    }
}
