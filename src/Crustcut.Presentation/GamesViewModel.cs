using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;

namespace Crustcut.Presentation;

public partial class GamesViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;

    private readonly GameRegistry _registry;
    private readonly GameProfileStore _profiles;
    private readonly ISteamGridDbClient _sgdb;
    private readonly ArtCache? _art;
    private readonly SteamCdnCoverFetcher? _steamCdn;
    private readonly ISteamAppLookup? _steamLookup;
    private bool _autoLinkRan;
    // Memoize name → Steam AppID so re-opening the Library doesn't re-hit Steam's search
    // API for every non-Steam game each time (risks throttling + slow cover loads).
    private readonly Dictionary<string, string?> _resolvedAppIdByName = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showApiKeyPrompt;
    [ObservableProperty] private bool _showEmptyState;

    /// <summary>Only Steam exists on macOS; naming six Windows launchers there is noise.</summary>
    public string DetectionSourcesText => OperatingSystem.IsWindows()
        ? "Detected from Steam, Xbox, Epic, Ubisoft, EA and GOG."
        : "Detected from your Steam library.";

    public ObservableCollection<GameTileViewModel> Tiles { get; } = new();

    public GamesViewModel(
        GameRegistry registry,
        GameProfileStore profiles,
        ISteamGridDbClient sgdb,
        ArtCache? artCache,
        SteamCdnCoverFetcher? steamCdn,
        ISteamAppLookup? steamLookup,
        IUiDispatcher ui)
    {
        _ui = ui;

        _registry = registry;
        _profiles = profiles;
        _sgdb = sgdb;
        _art = artCache;
        _steamCdn = steamCdn;
        _steamLookup = steamLookup;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Tiles.Clear();

        // Entire data phase off-thread (scan + one profile lookup per game); only the
        // collection mutations run back on the UI thread.
        var (games, tiles) = await Task.Run(async () =>
        {
            var all = await _registry.GetAllAsync();
            var built = new List<GameTileViewModel>(all.Count);
            foreach (var g in all)
            {
                var tile = new GameTileViewModel(g);
                var assigned = await _profiles.GetProfileForAsync(g.Id);
                tile.AssignedMode = assigned ?? "(none)";
                built.Add(tile);
            }
            return (all, built);
        });

        foreach (var tile in tiles) Tiles.Add(tile);

        // Only nag about the SGDB key if the user has at least one non-Steam game in
        // their library — every Steam game is already covered by the CDN fetcher.
        var hasNonSteamGame = games.Any(g => string.IsNullOrEmpty(g.SteamAppId));
        ShowApiKeyPrompt = hasNonSteamGame && !_sgdb.HasApiKey;
        ShowEmptyState = Tiles.Count == 0;
        IsLoading = false;

        _ = LoadCoversAsync();
        _ = AutoLinkUnmatchedAsync();
    }

    /// <summary>
    /// Background pass: for any manually-added game that has no Steam AppID, resolve
    /// by name and persist the match. Runs once per VM lifetime; affects the next
    /// library load (and the next game-start in Sentinel).
    /// </summary>
    private async Task AutoLinkUnmatchedAsync()
    {
        if (_autoLinkRan || _steamLookup is null) return;
        _autoLinkRan = true;

        try
        {
            var linked = await _registry.AutoLinkUnmatchedAsync(_steamLookup);
            if (linked > 0)
            {
                // Quietly refresh the tile list so the Steam-CDN cover fetcher and
                // Sentinel get the newly-attached AppIDs without needing a restart.
                _ui.Post(async () => await LoadAsync());
            }
        }
        catch { /* Network failure or partial match — leave the rest unlinked. */ }
    }

    private async Task LoadCoversAsync()
    {
        if (_art is null) return;

        foreach (var tile in Tiles.ToList())
        {
            try
            {
                string? path = null;
                var appId = tile.Game.SteamAppId;

                // Xbox / Epic / manually-added games have no Steam AppID. Resolve one by
                // name so they get free Steam-CDN cover art (most are on Steam too), no
                // SteamGridDB key required. Memoized so re-opening the Library is cheap.
                if (string.IsNullOrEmpty(appId) && _steamLookup is not null)
                {
                    var key = tile.Game.DisplayName;
                    if (_resolvedAppIdByName.TryGetValue(key, out var cached))
                    {
                        appId = cached;
                    }
                    else
                    {
                        try { appId = (await _steamLookup.ResolveAsync(key))?.AppId; }
                        catch { appId = null; /* name didn't resolve — fall through to SGDB / blank */ }
                        _resolvedAppIdByName[key] = appId;
                    }
                }

                // Primary: Steam's public CDN. No API key, no rate limits.
                if (_steamCdn is not null && !string.IsNullOrEmpty(appId))
                    path = await _steamCdn.FetchCoverAsync(appId);

                // Fallback: SteamGridDB lookup-by-name for games without a Steam app id
                // (manually-added EXEs, Epic-only titles). Only available if the user
                // added a SteamGridDB API key.
                if (path is null && _sgdb.HasApiKey)
                {
                    CoverArt art;
                    if (tile.Game.SteamAppId is not null)
                        art = await _sgdb.GetCoverByAppIdAsync(tile.Game.SteamAppId);
                    else
                    {
                        var hits = await _sgdb.SearchAsync(tile.Game.DisplayName);
                        var first = hits.FirstOrDefault();
                        art = first is null
                            ? new CoverArt(null, tile.Game.DisplayName, null, null)
                            : await _sgdb.GetCoverByGameIdAsync(first.Id, first.Name);
                    }

                    if (art.GameId is not null && art.Url is not null)
                        path = await _art.GetOrDownloadAsync(art.GameId.Value, art.Url);
                }

                                var finalPath = path;
                _ui.Post(() => { tile.CoverImagePath = finalPath; tile.IsLoadingCover = false; });
            }
            catch
            {
                tile.IsLoadingCover = false;
            }
        }
    }
}
