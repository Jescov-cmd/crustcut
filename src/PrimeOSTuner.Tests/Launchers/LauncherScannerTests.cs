using System.IO;
using System.Text.Json;
using FluentAssertions;
using PrimeOSTuner.Win.Launchers;
using Xunit;

namespace PrimeOSTuner.Tests.Launchers;

public class LauncherScannerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"launch-{Guid.NewGuid():N}");
    public LauncherScannerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    // ---- LauncherExe heuristic ----
    [Fact]
    public void LauncherExe_picks_largest_real_exe_and_skips_helpers()
    {
        File.WriteAllText(Path.Combine(_dir, "EALaunchHelper.exe"), new string('x', 10));
        File.WriteAllText(Path.Combine(_dir, "UnityCrashHandler64.exe"), new string('x', 10));
        File.WriteAllText(Path.Combine(_dir, "StarWarsBattlefrontII.exe"), new string('x', 9000));

        LauncherExe.FindPrimary(_dir).Should().Be(Path.Combine(_dir, "StarWarsBattlefrontII.exe"));
    }

    // ---- Epic ----
    [Fact]
    public void Epic_ParseManifest_reads_name_and_resolves_launch_executable()
    {
        var install = Path.Combine(_dir, "rocketleague");
        var binDir = Path.Combine(install, "Binaries", "Win64");
        Directory.CreateDirectory(binDir);
        var exe = Path.Combine(binDir, "RocketLeague.exe");
        File.WriteAllText(exe, "game");

        var manifest = Path.Combine(_dir, "rl.item");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            DisplayName = "Rocket League",
            InstallLocation = install,
            LaunchExecutable = "Binaries/Win64/RocketLeague.exe",
            AppName = "Sugar"
        }));

        var game = EpicGameScanner.ParseManifest(manifest);
        game.Should().NotBeNull();
        game!.Name.Should().Be("Rocket League");
        game.Launcher.Should().Be(GameLauncher.Epic);
        Path.GetFileName(game.PrimaryExecutablePath).Should().Be("RocketLeague.exe");
    }

    [Fact]
    public void Epic_ParseManifest_returns_null_for_garbage()
    {
        var bad = Path.Combine(_dir, "bad.item");
        File.WriteAllText(bad, "{ not json");
        EpicGameScanner.ParseManifest(bad).Should().BeNull();
    }

    // ---- Ubisoft ----
    [Fact]
    public void Ubisoft_BuildGame_uses_folder_name_and_resolves_exe()
    {
        var install = Path.Combine(_dir, "Tom Clancy's Rainbow Six Siege");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "RainbowSix.exe"), new string('x', 9000));
        File.WriteAllText(Path.Combine(install, "uplay_installer.exe"), new string('x', 10));

        var game = UbisoftGameScanner.BuildGame("635", install + "/");   // trailing slash like the registry
        game.Should().NotBeNull();
        game!.Name.Should().Be("Tom Clancy's Rainbow Six Siege");
        game.Launcher.Should().Be(GameLauncher.Ubisoft);
        Path.GetFileName(game.PrimaryExecutablePath).Should().Be("RainbowSix.exe");
    }

    [Fact]
    public void Ubisoft_BuildGame_returns_null_for_empty_dir_value()
    {
        UbisoftGameScanner.BuildGame("1", null).Should().BeNull();
        UbisoftGameScanner.BuildGame("1", "   ").Should().BeNull();
    }

    // ---- GOG ----
    [Fact]
    public void Gog_BuildGame_resolves_relative_exe_against_path()
    {
        var install = Path.Combine(_dir, "Witcher3");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "witcher3.exe"), "game");

        var game = GogGameScanner.BuildGame("1207664663", "The Witcher 3", install, "witcher3.exe");
        game.Should().NotBeNull();
        game!.Name.Should().Be("The Witcher 3");
        game.Launcher.Should().Be(GameLauncher.Gog);
        Path.GetFileName(game.PrimaryExecutablePath).Should().Be("witcher3.exe");
    }

    // ---- EA (injected roots) ----
    [Fact]
    public void Ea_scans_injected_roots_and_finds_games()
    {
        var root = Path.Combine(_dir, "EA Games");
        var game = Path.Combine(root, "STAR WARS Battlefront II");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "starwarsbattlefrontii.exe"), new string('x', 9000));
        File.WriteAllText(Path.Combine(game, "cleanup.exe"), new string('x', 10));

        var scanner = new EaGameScanner(new[] { root });
        var games = scanner.Scan();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("STAR WARS Battlefront II");
        games[0].Launcher.Should().Be(GameLauncher.Ea);
        Path.GetFileName(games[0].PrimaryExecutablePath).Should().Be("starwarsbattlefrontii.exe");
    }

    [Fact]
    public void Ea_skips_an_installer_stub_with_no_resolvable_exe()
    {
        var root = Path.Combine(_dir, "EA Games");
        var installer = Path.Combine(root, "Phantom Game", "__Installer");
        Directory.CreateDirectory(installer);
        File.WriteAllText(Path.Combine(installer, "installerdata.xml"),
            "<DiPManifest><runtime><launcher>" +
            "<filePath>[HKEY_LOCAL_MACHINE\\SOFTWARE\\EA Games\\Phantom\\Install Dir]phantom.exe</filePath>" +
            "</launcher></runtime></DiPManifest>");

        // No exe in the folder and the referenced registry key doesn't exist → not installed.
        new EaGameScanner(new[] { root }).Scan().Should().BeEmpty();
    }

    [Fact]
    public void All_scanners_scan_without_throwing_on_real_machine()
    {
        ((System.Action)(() => new EpicGameScanner().Scan())).Should().NotThrow();
        ((System.Action)(() => new UbisoftGameScanner().Scan())).Should().NotThrow();
        ((System.Action)(() => new EaGameScanner().Scan())).Should().NotThrow();
        ((System.Action)(() => new GogGameScanner().Scan())).Should().NotThrow();
    }
}
