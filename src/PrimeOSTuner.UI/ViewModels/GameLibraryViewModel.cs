using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.SteamGridDb;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameLibraryViewModel : ObservableObject
{
    private readonly GameRegistry _registry;
    private readonly GameProfileStore _profiles;
    private readonly ISteamGridDbClient _sgdb;
    private readonly ArtCache? _art;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showApiKeyPrompt;

    public ObservableCollection<GameTileViewModel> Tiles { get; } = new();

    public GameLibraryViewModel(
        GameRegistry registry,
        GameProfileStore profiles,
        ISteamGridDbClient sgdb,
        ArtCache? artCache)
    {
        _registry = registry;
        _profiles = profiles;
        _sgdb = sgdb;
        _art = artCache;
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

        ShowApiKeyPrompt = !_sgdb.HasApiKey;
        IsLoading = false;

        _ = LoadCoversAsync();
    }

    private async Task LoadCoversAsync()
    {
        if (_art is null || !_sgdb.HasApiKey) return;
        foreach (var tile in Tiles.ToList())
        {
            try
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
                {
                    var path = await _art.GetOrDownloadAsync(art.GameId.Value, art.Url);
                    var dispatcher = Application.Current?.Dispatcher;
                    Action update = () => { tile.CoverImagePath = path; tile.IsLoadingCover = false; };
                    if (dispatcher is null || dispatcher.CheckAccess()) update();
                    else dispatcher.Invoke(update);
                }
                else
                {
                    tile.IsLoadingCover = false;
                }
            }
            catch
            {
                tile.IsLoadingCover = false;
            }
        }
    }
}
