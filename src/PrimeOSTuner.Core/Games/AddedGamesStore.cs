using System.Text.Json;

namespace PrimeOSTuner.Core.Games;

public sealed class AddedGamesStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AddedGamesStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "added-games.json");

    public async Task<IReadOnlyList<KnownGame>> LoadAsync()
    {
        if (!File.Exists(_path)) return Array.Empty<KnownGame>();
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<List<KnownGame>>(json) ?? new List<KnownGame>();
        }
        catch
        {
            return Array.Empty<KnownGame>();
        }
    }

    public async Task AddAsync(KnownGame game)
    {
        var list = (await LoadAsync()).ToList();
        list.RemoveAll(g => g.Id == game.Id);
        list.Add(game);
        await SaveAsync(list);
    }

    public async Task RemoveAsync(string id)
    {
        var list = (await LoadAsync()).Where(g => g.Id != id).ToList();
        await SaveAsync(list);
    }

    private async Task SaveAsync(List<KnownGame> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(list, JsonOpts));
    }
}
