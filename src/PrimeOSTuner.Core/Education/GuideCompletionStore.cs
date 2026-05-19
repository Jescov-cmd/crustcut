using System.Text.Json;

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
        if (!File.Exists(_path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(IEnumerable<string> completedIds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(completedIds.ToList(), JsonOpts);
        await File.WriteAllTextAsync(_path, json);
    }
}
