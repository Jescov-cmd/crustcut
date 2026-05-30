using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Profiles;

public sealed class CustomProfileStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public CustomProfileStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "custom-profile.json");

    public async Task<ModeProfile> LoadAsync()
    {
        var json = await ResilientJsonFile.ReadTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json))
            return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", Array.Empty<string>());

        List<string> ids;
        try { ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { ids = new List<string>(); }
        return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", ids);
    }

    public async Task SaveAsync(IEnumerable<string> tweakIds)
    {
        var json = JsonSerializer.Serialize(tweakIds.ToList(), JsonOpts);
        await ResilientJsonFile.WriteTextAsync(_path, json);
    }
}
