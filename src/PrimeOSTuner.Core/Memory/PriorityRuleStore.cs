using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Memory;

public sealed class PriorityRuleStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PriorityRuleStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner",
        "priority-rules.json");

    public async Task<IReadOnlyList<PriorityRule>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return Array.Empty<PriorityRule>();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<PriorityRule>();
            return JsonSerializer.Deserialize<List<PriorityRule>>(json, Opts)
                ?? new List<PriorityRule>();
        }
        catch (JsonException)
        {
            return Array.Empty<PriorityRule>();
        }
    }

    public async Task SaveAsync(IEnumerable<PriorityRule> rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(rules, Opts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
