using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Education;

/// <summary>
/// Persists which "Optimization 101" guides the user has marked as done.
/// Stored as a JSON array of guide ids in the per-user app-data folder.
/// </summary>
public sealed class GuideCompletionStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GuideCompletionStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "guide-completion.json");

    public async Task<IReadOnlySet<string>> LoadAsync()
    {
        var json = await ResilientJsonFile.ReadTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task SaveAsync(IEnumerable<string> completedIds)
    {
        var json = JsonSerializer.Serialize(completedIds.ToList(), JsonOpts);
        await ResilientJsonFile.WriteTextAsync(_path, json);
    }
}
