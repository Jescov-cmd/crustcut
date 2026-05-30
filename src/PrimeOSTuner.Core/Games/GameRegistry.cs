using PrimeOSTuner.Win.Launchers;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.Xbox;

namespace PrimeOSTuner.Core.Games;

public sealed class GameRegistry
{
    private readonly ISteamLibraryScanner _steam;
    private readonly IXboxLibraryScanner _xbox;
    private readonly IReadOnlyList<IExternalGameScanner> _launchers;
    private readonly AddedGamesStore _added;

    public GameRegistry(
        ISteamLibraryScanner steam,
        IXboxLibraryScanner xbox,
        IEnumerable<IExternalGameScanner> launchers,
        AddedGamesStore added)
    {
        _steam = steam;
        _xbox = xbox;
        _launchers = launchers.ToList();
        _added = added;
    }

    private static KnownGameSource MapSource(GameLauncher launcher) => launcher switch
    {
        GameLauncher.Epic => KnownGameSource.Epic,
        GameLauncher.Ubisoft => KnownGameSource.Ubisoft,
        GameLauncher.Ea => KnownGameSource.Ea,
        GameLauncher.Gog => KnownGameSource.Gog,
        _ => KnownGameSource.UserAdded
    };

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

    // The disk/registry scans (Steam, Xbox, launchers, static catalog) change rarely but
    // GetAllAsync is polled every ~2s by the game watcher. Cache the scan portion for a
    // short window so we're not hammering the disk and registry. User-added games are NOT
    // cached — they're a quick file read and must reflect edits (e.g. auto-link) instantly.
    private static readonly TimeSpan ScanCacheTtl = TimeSpan.FromSeconds(20);
    private readonly object _cacheGate = new();
    private IReadOnlyList<KnownGame>? _scanCache;
    private DateTime _scanCacheUtc;

    public async Task<IReadOnlyList<KnownGame>> GetAllAsync()
    {
        var result = new List<KnownGame>(GetScannedGames());
        foreach (var g in await _added.LoadAsync())
            result.Add(g);
        return result;
    }

    private IReadOnlyList<KnownGame> GetScannedGames()
    {
        lock (_cacheGate)
        {
            if (_scanCache is not null && DateTime.UtcNow - _scanCacheUtc < ScanCacheTtl)
                return _scanCache;
        }

        var built = BuildScannedGames();

        lock (_cacheGate)
        {
            _scanCache = built;
            _scanCacheUtc = DateTime.UtcNow;
        }
        return built;
    }

    private IReadOnlyList<KnownGame> BuildScannedGames()
    {
        var result = new List<KnownGame>();
        var seenSteamIds = new HashSet<string>();

        foreach (var sg in _steam.ScanInstalledGames())
        {
            result.Add(new KnownGame(
                Id: $"steam.{sg.AppId}",
                DisplayName: sg.Name,
                ExecutableNames: ExeNames(sg.PrimaryExecutablePath),
                SteamAppId: sg.AppId,
                InstallPath: sg.PrimaryExecutablePath,
                Source: KnownGameSource.Steam));
            seenSteamIds.Add(sg.AppId);
        }

        // Xbox app / Game Pass (PC) games — discovered on disk under <drive>:\XboxGames.
        foreach (var xg in _xbox.ScanInstalledGames())
        {
            if (IsSameGameAlreadyListed(result, xg.Name, xg.PrimaryExecutablePath)) continue;
            result.Add(new KnownGame(
                Id: xg.Id,
                DisplayName: CleanTitle(xg.Name),
                ExecutableNames: ExeNames(xg.PrimaryExecutablePath),
                SteamAppId: null,
                InstallPath: xg.PrimaryExecutablePath,
                Source: KnownGameSource.Xbox));
        }

        // External launchers — Epic, Ubisoft, EA, GOG (each a registered IExternalGameScanner).
        foreach (var scanner in _launchers)
        {
            IReadOnlyList<ExternalGame> scanned;
            try { scanned = scanner.Scan(); }
            catch { continue; }   // one launcher failing must never break the others

            foreach (var eg in scanned)
            {
                if (IsSameGameAlreadyListed(result, eg.Name, eg.PrimaryExecutablePath)) continue;
                result.Add(new KnownGame(
                    Id: eg.Id,
                    DisplayName: CleanTitle(eg.Name),
                    ExecutableNames: ExeNames(eg.PrimaryExecutablePath),
                    SteamAppId: null,
                    InstallPath: eg.PrimaryExecutablePath,
                    Source: MapSource(eg.Launcher)));
            }
        }

        foreach (var g in StaticGameCatalog.All)
        {
            if (g.SteamAppId is not null && seenSteamIds.Contains(g.SteamAppId)) continue;
            result.Add(g);
        }

        return result;
    }

    private static string[] ExeNames(string? exePath)
    {
        var name = exePath is not null ? Path.GetFileName(exePath) : null;
        return string.IsNullOrEmpty(name) ? Array.Empty<string>() : new[] { name };
    }

    /// <summary>
    /// True only if the SAME game (same exe filename AND a matching title) is already in the
    /// list — i.e. owned on two stores. Requiring BOTH avoids dropping two genuinely
    /// different games that happen to share a generic exe name (e.g. "ShooterGame.exe").
    /// </summary>
    private static bool IsSameGameAlreadyListed(List<KnownGame> existing, string name, string? exePath)
    {
        var exeName = exePath is not null ? Path.GetFileName(exePath) : null;
        if (string.IsNullOrEmpty(exeName)) return false;

        var norm = Normalize(name);
        return existing.Any(e =>
            e.ExecutableNames.Contains(exeName, StringComparer.OrdinalIgnoreCase)
            && TitlesMatch(norm, Normalize(e.DisplayName)));
    }

    // Strip trademark/registered/copyright glyphs (e.g. Epic's "Rocket League®") so titles
    // display cleanly and match Steam's cover-art name search.
    private static string CleanTitle(string name)
        => (name ?? "").Replace("®", "").Replace("™", "").Replace("©", "").Trim();

    private static string Normalize(string s)
        => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool TitlesMatch(string a, string b)
    {
        if (a.Length < 3 || b.Length < 3) return a == b;     // guard against trivial substrings
        return a == b || a.Contains(b) || b.Contains(a);
    }
}
