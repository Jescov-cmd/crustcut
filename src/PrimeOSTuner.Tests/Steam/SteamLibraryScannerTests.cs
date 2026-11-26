using FluentAssertions;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.Steam;

public class SteamLibraryScannerTests
{
    [Fact]
    public void ParseLibraryFolders_returns_each_path_in_file()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "libraryfolders.vdf");
        File.Exists(fixturePath).Should().BeTrue("fixture must be copied to output");

        var paths = SteamLibraryScanner.ParseLibraryFolders(fixturePath);

        paths.Should().Contain(@"C:\Program Files (x86)\Steam");
        paths.Should().Contain(@"D:\SteamLibrary");
    }

    [Fact]
    public void ParseAppManifest_extracts_appid_name_and_installdir()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "appmanifest_440.acf");

        var game = SteamLibraryScanner.ParseAppManifest(fixturePath, libraryPath: @"C:\Program Files (x86)\Steam");

        game.Should().NotBeNull();
        game!.AppId.Should().Be("440");
        game.Name.Should().Be("Team Fortress 2");
        game.InstallDir.Should().Be("Team Fortress 2");
    }

    [Fact]
    public void ParseAppManifest_returns_null_when_file_missing()
    {
        var game = SteamLibraryScanner.ParseAppManifest(@"C:\does\not\exist.acf", libraryPath: @"C:\");
        game.Should().BeNull();
    }

    [Theory]
    [InlineData("Steamworks Common Redistributables")]
    [InlineData("Lords of the Fallen Soundtrack")]
    [InlineData("Proton Experimental")]
    [InlineData("ARK Survival Evolved Dedicated Server")]
    [InlineData("Hollow Knight OST")]
    // Tray tools bought on Steam: counted as games, they made Crustcut think a game was
    // running whenever they were open (the user has all three).
    [InlineData("Lossless Scaling")]
    [InlineData("Crosshair X")]
    [InlineData("Mouse X")]
    public void Non_games_steam_installs_are_skipped(string name)
        => SteamLibraryScanner.IsNotAGame(name).Should().BeTrue();

    [Theory]
    [InlineData("Ghost of Tsushima")]      // "OST" inside a word is not a soundtrack
    [InlineData("Terraria")]
    [InlineData("Lords of the Fallen")]
    public void Real_games_are_kept(string name)
        => SteamLibraryScanner.IsNotAGame(name).Should().BeFalse();

    /// <summary>The user's Terraria: the bundled .NET installer sorted first and was taken
    /// as "the game", so Terraria was never detected.</summary>
    [Fact]
    public void An_installer_next_to_the_game_is_never_taken_as_the_game()
    {
        var picked = SteamLibraryScanner.PickExecutables(new[]
        {
            @"C:\steamapps\common\Terraria\dotNetFx40_Full_setup.exe",
            @"C:\steamapps\common\Terraria\Terraria.exe",
            @"C:\steamapps\common\Terraria\TerrariaServer.exe",
        }, "Terraria", "Terraria");

        picked.Should().NotContain(p => p.Contains("setup"));
        picked[0].Should().EndWith("Terraria.exe");
    }

    [Fact]
    public void Unreal_games_rank_the_real_shipping_exe_first()
    {
        var picked = SteamLibraryScanner.PickExecutables(new[]
        {
            @"C:\common\Lords of the Fallen\LOTF2.exe",
            @"C:\common\Lords of the Fallen\LOTF2\Binaries\Win64\LOTF2-Win64-Shipping.exe",
            @"C:\common\Lords of the Fallen\Engine\Binaries\Win64\CrashReportClient.exe",
        }, "Lords of the Fallen", "Lords of the Fallen");

        picked.Should().HaveCount(2).And.NotContain(p => p.Contains("CrashReport"));
        picked[0].Should().EndWith("LOTF2-Win64-Shipping.exe");
    }
}
