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
}
