using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win.SteamGridDb;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class GameLibraryViewModelTests : IDisposable
{
    private readonly string _addedPath = Path.Combine(Path.GetTempPath(), $"vm-added-{Guid.NewGuid()}.json");
    private readonly string _gpPath = Path.Combine(Path.GetTempPath(), $"vm-gp-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        foreach (var p in new[] { _addedPath, _gpPath })
            if (File.Exists(p)) File.Delete(p);
    }

    [Fact]
    public async Task LoadAsync_populates_tiles_for_each_game_in_registry()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var registry = new GameRegistry(scanner.Object, new AddedGamesStore(_addedPath));
        var profileStore = new GameProfileStore(_gpPath);
        var sgdb = new Mock<ISteamGridDbClient>();
        sgdb.SetupGet(c => c.HasApiKey).Returns(false);

        var vm = new GameLibraryViewModel(registry, profileStore, sgdb.Object, artCache: null, steamCdn: null);
        await vm.LoadAsync();

        vm.Tiles.Should().NotBeEmpty();
        vm.Tiles.Should().Contain(t => t.Id == "static.valorant");
    }

    [Fact]
    public async Task LoadAsync_attaches_assigned_mode_from_profile_store()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var registry = new GameRegistry(scanner.Object, new AddedGamesStore(_addedPath));
        var profileStore = new GameProfileStore(_gpPath);
        await profileStore.SetProfileForAsync("static.valorant", "performance");
        var sgdb = new Mock<ISteamGridDbClient>();
        sgdb.SetupGet(c => c.HasApiKey).Returns(false);

        var vm = new GameLibraryViewModel(registry, profileStore, sgdb.Object, artCache: null, steamCdn: null);
        await vm.LoadAsync();

        var tile = vm.Tiles.First(t => t.Id == "static.valorant");
        tile.AssignedMode.Should().Be("performance");
    }
}
