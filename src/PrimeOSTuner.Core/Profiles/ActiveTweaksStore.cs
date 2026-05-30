using System.Text.Json;
using PrimeOSTuner.Core.Storage;

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
        var json = await ResilientJsonFile.ReadTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ActiveTweaksRecord>(json); }
        catch (JsonException) { return null; }
    }

    public async Task SaveAsync(ActiveTweaksRecord record)
    {
        var json = JsonSerializer.Serialize(record, JsonOpts);
        await ResilientJsonFile.WriteTextAsync(_path, json);
    }

    public Task ClearAsync() => ResilientJsonFile.DeleteAsync(_path);
}
