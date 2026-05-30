using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Launchers;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.Xbox;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class GameRegistryTests : IDisposable
{
    private readonly string _addedPath = Path.Combine(Path.GetTempPath(), $"reg-added-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_addedPath)) File.Delete(_addedPath); }

    private static IXboxLibraryScanner NoXbox()
    {
        var m = new Mock<IXboxLibraryScanner>();
        m.Setup(x => x.ScanInstalledGames()).Returns(Array.Empty<XboxGame>());
        return m.Object;
    }

    private static IEnumerable<IExternalGameScanner> NoLaunchers() => Array.Empty<IExternalGameScanner>();

    private static IExternalGameScanner Launcher(GameLauncher which, params ExternalGame[] games)
    {
        var m = new Mock<IExternalGameScanner>();
        m.SetupGet(s => s.Launcher).Returns(which);
        m.Setup(s => s.Scan()).Returns(games);
        return m.Object;
    }

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

        var registry = new GameRegistry(scanner.Object, NoXbox(), NoLaunchers(), added);
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
        var registry = new GameRegistry(scanner.Object, NoXbox(), NoLaunchers(), added);

        var all = await registry.GetAllAsync();

        all.Where(g => g.SteamAppId == "1172470").Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_includes_xbox_games_with_their_executable()
    {
        var steam = new Mock<ISteamLibraryScanner>();
        steam.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var xbox = new Mock<IXboxLibraryScanner>();
        xbox.Setup(x => x.ScanInstalledGames()).Returns(new[]
        {
            new XboxGame("xbox.Fortnite", "Fortnite", @"C:\XboxGames\Fortnite", @"C:\XboxGames\Fortnite\Content\FortniteClient.exe")
        });
        var added = new AddedGamesStore(_addedPath);

        var registry = new GameRegistry(steam.Object, xbox.Object, NoLaunchers(), added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g =>
            g.Source == KnownGameSource.Xbox &&
            g.DisplayName == "Fortnite" &&
            g.ExecutableNames.Contains("FortniteClient.exe"));
    }

    [Fact]
    public async Task GetAllAsync_does_not_double_list_a_game_present_in_both_steam_and_xbox()
    {
        var steam = new Mock<ISteamLibraryScanner>();
        steam.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("0", "Remnant 2", "Remnant2", @"C:\Steam", @"C:\Steam\Remnant2.exe")
        });
        var xbox = new Mock<IXboxLibraryScanner>();
        xbox.Setup(x => x.ScanInstalledGames()).Returns(new[]
        {
            new XboxGame("xbox.Remnant 2", "Remnant 2", @"C:\XboxGames\Remnant 2", @"C:\XboxGames\Remnant 2\Content\Remnant2.exe")
        });
        var added = new AddedGamesStore(_addedPath);

        var registry = new GameRegistry(steam.Object, xbox.Object, NoLaunchers(), added);
        var all = await registry.GetAllAsync();

        all.Where(g => g.ExecutableNames.Contains("Remnant2.exe")).Should().HaveCount(1, "same exe shouldn't be listed twice");
    }

    [Fact]
    public async Task GetAllAsync_keeps_two_different_games_that_share_a_generic_exe_name()
    {
        // Regression: dedup-by-exe-name-only would drop one of these. Two distinct games
        // both shipping "ShooterGame.exe" must BOTH survive (different titles).
        var steam = new Mock<ISteamLibraryScanner>();
        steam.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("1", "Squad", "Squad", @"C:\Steam", @"C:\Steam\Squad\ShooterGame.exe")
        });
        var launchers = new[]
        {
            Launcher(GameLauncher.Epic, new ExternalGame("epic.ins", "Insurgency Sandstorm", @"C:\Epic\Ins\ShooterGame.exe", GameLauncher.Epic)),
        };
        var added = new AddedGamesStore(_addedPath);

        var registry = new GameRegistry(steam.Object, NoXbox(), launchers, added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g => g.DisplayName == "Squad");
        all.Should().Contain(g => g.DisplayName == "Insurgency Sandstorm");
        all.Where(g => g.ExecutableNames.Contains("ShooterGame.exe")).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_includes_games_from_external_launchers_mapped_to_the_right_source()
    {
        var steam = new Mock<ISteamLibraryScanner>();
        steam.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var added = new AddedGamesStore(_addedPath);

        var launchers = new[]
        {
            Launcher(GameLauncher.Epic, new ExternalGame("epic.rl", "Rocket League", @"C:\Epic\rl\rl.exe", GameLauncher.Epic)),
            Launcher(GameLauncher.Ubisoft, new ExternalGame("ubi.635", "Rainbow Six Siege", @"C:\Ubi\R6\RainbowSix.exe", GameLauncher.Ubisoft)),
            Launcher(GameLauncher.Ea, new ExternalGame("ea.bf2", "Battlefront II", @"C:\EA\bf2\bf2.exe", GameLauncher.Ea)),
        };

        var registry = new GameRegistry(steam.Object, NoXbox(), launchers, added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g => g.Source == KnownGameSource.Epic && g.DisplayName == "Rocket League" && g.ExecutableNames.Contains("rl.exe"));
        all.Should().Contain(g => g.Source == KnownGameSource.Ubisoft && g.DisplayName == "Rainbow Six Siege" && g.ExecutableNames.Contains("RainbowSix.exe"));
        all.Should().Contain(g => g.Source == KnownGameSource.Ea && g.DisplayName == "Battlefront II");
    }

    [Fact]
    public async Task GetAllAsync_survives_a_launcher_scanner_that_throws()
    {
        var steam = new Mock<ISteamLibraryScanner>();
        steam.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var boom = new Mock<IExternalGameScanner>();
        boom.SetupGet(s => s.Launcher).Returns(GameLauncher.Gog);
        boom.Setup(s => s.Scan()).Throws(new InvalidOperationException("registry blew up"));
        var ok = Launcher(GameLauncher.Epic, new ExternalGame("epic.x", "X", @"C:\x\x.exe", GameLauncher.Epic));
        var added = new AddedGamesStore(_addedPath);

        var registry = new GameRegistry(steam.Object, NoXbox(), new[] { boom.Object, ok }, added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g => g.Id == "epic.x", "a throwing launcher must not break the others");
    }
}
