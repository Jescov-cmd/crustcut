using System.Text.Json;
using PrimeOSTuner.Core.Storage;

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

    private readonly System.Threading.SemaphoreSlim _rmw = new(1, 1);

    public async Task<IReadOnlyList<KnownGame>> LoadAsync()
    {
        try
        {
            var json = await ResilientJsonFile.ReadTextAsync(_path);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<KnownGame>();
            return JsonSerializer.Deserialize<List<KnownGame>>(json) ?? new List<KnownGame>();
        }
        catch
        {
            return Array.Empty<KnownGame>();
        }
    }

    public async Task AddAsync(KnownGame game)
    {
        await _rmw.WaitAsync();
        try
        {
            var list = (await LoadAsync()).ToList();
            list.RemoveAll(g => g.Id == game.Id);
            list.Add(game);
            await SaveAsync(list);
        }
        finally { _rmw.Release(); }
    }

    public async Task RemoveAsync(string id)
    {
        await _rmw.WaitAsync();
        try
        {
            var list = (await LoadAsync()).Where(g => g.Id != id).ToList();
            await SaveAsync(list);
        }
        finally { _rmw.Release(); }
    }

    private async Task SaveAsync(List<KnownGame> list)
        => await ResilientJsonFile.WriteTextAsync(_path, JsonSerializer.Serialize(list, JsonOpts));
}
