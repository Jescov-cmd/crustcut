using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Games;

public sealed class GameProfileStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GameProfileStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "game-profiles.json");

    public async Task<IReadOnlyList<GameProfileAssignment>> LoadAsync()
    {
        try
        {
            var json = await ResilientJsonFile.ReadTextAsync(_path);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<GameProfileAssignment>();
            return JsonSerializer.Deserialize<List<GameProfileAssignment>>(json) ?? new();
        }
        catch
        {
            return Array.Empty<GameProfileAssignment>();
        }
    }

    public async Task<string?> GetProfileForAsync(string gameId)
    {
        var entries = await LoadAsync();
        return entries.FirstOrDefault(e => e.GameId == gameId)?.ModeName;
    }

    public async Task SetProfileForAsync(string gameId, string modeName)
    {
        var list = (await LoadAsync()).Where(e => e.GameId != gameId).ToList();
        list.Add(new GameProfileAssignment(gameId, modeName));
        await SaveAsync(list);
    }

    public async Task ClearProfileForAsync(string gameId)
    {
        var list = (await LoadAsync()).Where(e => e.GameId != gameId).ToList();
        await SaveAsync(list);
    }

    private async Task SaveAsync(List<GameProfileAssignment> list)
        => await ResilientJsonFile.WriteTextAsync(_path, JsonSerializer.Serialize(list, JsonOpts));
}
