using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class GameRegistryTests : IDisposable
{
    private readonly string _addedPath = Path.Combine(Path.GetTempPath(), $"reg-added-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_addedPath)) File.Delete(_addedPath); }

    [Fact]
    public async Task GetAllAsync_returns_steam_static_and_user_added_combined()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("440", "Team Fortress 2", "Team Fortress 2", @"C:\Steam", @"C:\Steam\tf2.exe")
        });
        var added = new AddedGamesStore(_addedPath);
        await added.AddAsync(new KnownGame("user.x", "Custom Game", new[] { "x.exe" }, null, null, KnownGameSource.UserAdded));

        var registry = new GameRegistry(scanner.Object, added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g => g.Source == KnownGameSource.Steam && g.DisplayName == "Team Fortress 2");
        all.Should().Contain(g => g.Source == KnownGameSource.StaticCatalog && g.Id == "static.valorant");
        all.Should().Contain(g => g.Source == KnownGameSource.UserAdded && g.Id == "user.x");
    }

    [Fact]
    public async Task GetAllAsync_de_duplicates_when_static_game_is_also_in_steam()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("1172470", "Apex Legends", "Apex Legends", @"C:\Steam", null)
        });
        var added = new AddedGamesStore(_addedPath);
        var registry = new GameRegistry(scanner.Object, added);

        var all = await registry.GetAllAsync();

        all.Where(g => g.SteamAppId == "1172470").Should().HaveCount(1);
    }
}
