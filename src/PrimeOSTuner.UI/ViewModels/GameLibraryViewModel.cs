using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameLibraryViewModel : ObservableObject
{
    private readonly GameRegistry _registry;
    private readonly GameProfileStore _profiles;
    private readonly ISteamGridDbClient _sgdb;
    private readonly ArtCache? _art;
    private readonly SteamCdnCoverFetcher? _steamCdn;
    private readonly ISteamAppLookup? _steamLookup;
    private bool _autoLinkRan;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showApiKeyPrompt;

    public ObservableCollection<GameTileViewModel> Tiles { get; } = new();

    public GameLibraryViewModel(
        GameRegistry registry,
        GameProfileStore profiles,
        ISteamGridDbClient sgdb,
        ArtCache? artCache,
        SteamCdnCoverFetcher? steamCdn,
        ISteamAppLookup? steamLookup = null)
    {
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

        var games = await _registry.GetAllAsync();
        foreach (var g in games)
        {
            var tile = new GameTileViewModel(g);
            var assigned = await _profiles.GetProfileForAsync(g.Id);
            tile.AssignedMode = assigned ?? "(none)";
            Tiles.Add(tile);
        }

        // Only nag about the SGDB key if the user has at least one non-Steam game in
        // their library — every Steam game is already covered by the CDN fetcher.
        var hasNonSteamGame = games.Any(g => string.IsNullOrEmpty(g.SteamAppId));
        ShowApiKeyPrompt = hasNonSteamGame && !_sgdb.HasApiKey;
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
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.CheckAccess()) await LoadAsync();
                else await dispatcher.InvokeAsync(async () => await LoadAsync());
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

                // Primary: Steam's public CDN. No API key, no rate limits.
                if (_steamCdn is not null && !string.IsNullOrEmpty(tile.Game.SteamAppId))
                    path = await _steamCdn.FetchCoverAsync(tile.Game.SteamAppId);

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

                var dispatcher = Application.Current?.Dispatcher;
                var finalPath = path;
                Action update = () => { tile.CoverImagePath = finalPath; tile.IsLoadingCover = false; };
                if (dispatcher is null || dispatcher.CheckAccess()) update();
                else dispatcher.Invoke(update);
            }
            catch
            {
                tile.IsLoadingCover = false;
            }
        }
    }
}
