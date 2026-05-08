using PrimeOSTuner.Win.Steam;

namespace PrimeOSTuner.Core.Games;

public sealed class GameRegistry
{
    private readonly ISteamLibraryScanner _steam;
    private readonly AddedGamesStore _added;

    public GameRegistry(ISteamLibraryScanner steam, AddedGamesStore added)
    {
        _steam = steam;
        _added = added;
    }

    public async Task<IReadOnlyList<KnownGame>> GetAllAsync()
    {
        var result = new List<KnownGame>();
        var seenSteamIds = new HashSet<string>();

        foreach (var sg in _steam.ScanInstalledGames())
        {
            var exeName = sg.PrimaryExecutablePath is not null
                ? Path.GetFileName(sg.PrimaryExecutablePath)
                : null;
            var exes = exeName is null ? Array.Empty<string>() : new[] { exeName };
            result.Add(new KnownGame(
                Id: $"steam.{sg.AppId}",
                DisplayName: sg.Name,
                ExecutableNames: exes,
                SteamAppId: sg.AppId,
                InstallPath: sg.PrimaryExecutablePath,
                Source: KnownGameSource.Steam));
            seenSteamIds.Add(sg.AppId);
        }

        foreach (var g in StaticGameCatalog.All)
        {
            if (g.SteamAppId is not null && seenSteamIds.Contains(g.SteamAppId)) continue;
            result.Add(g);
        }

        foreach (var g in await _added.LoadAsync())
            result.Add(g);

        return result;
    }
}
