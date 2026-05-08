using System.Text.Json;

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
        if (!File.Exists(_path))
            return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", Array.Empty<string>());

        var json = await File.ReadAllTextAsync(_path);
        var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", ids);
    }

    public async Task SaveAsync(IEnumerable<string> tweakIds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(tweakIds.ToList(), JsonOpts);
        await File.WriteAllTextAsync(_path, json);
    }
}
