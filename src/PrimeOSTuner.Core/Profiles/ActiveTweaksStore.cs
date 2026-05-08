using System.Text.Json;

namespace PrimeOSTuner.Core.Profiles;

public sealed class ActiveTweaksStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ActiveTweaksStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "active-tweaks.json");

    public async Task<ActiveTweaksRecord?> LoadAsync()
    {
        if (!File.Exists(_path)) return null;
        var json = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<ActiveTweaksRecord>(json);
    }

    public async Task SaveAsync(ActiveTweaksRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(record, JsonOpts);
        await File.WriteAllTextAsync(_path, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
