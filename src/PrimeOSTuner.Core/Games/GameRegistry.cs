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

    /// <summary>
    /// One-shot pass: for every manually-added game that has no Steam AppID, try
    /// resolving its name via the Steam Store search API and persist the match.
    /// Silent on failure — a game that doesn't resolve stays unlinked and Sentinel
    /// just runs without spec data for it. Returns the count of newly-linked games.
    /// </summary>
    public async Task<int> AutoLinkUnmatchedAsync(ISteamAppLookup lookup, CancellationToken ct = default)
    {
        var added = await _added.LoadAsync();
        var linked = 0;
        foreach (var g in added)
        {
            if (ct.IsCancellationRequested) break;
            if (!string.IsNullOrEmpty(g.SteamAppId)) continue;
            if (g.Source != KnownGameSource.UserAdded) continue;

            var match = await lookup.ResolveAsync(g.DisplayName, ct);
            if (match is null) continue;

            await _added.AddAsync(g with { SteamAppId = match.AppId });
            linked++;
        }
        return linked;
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
