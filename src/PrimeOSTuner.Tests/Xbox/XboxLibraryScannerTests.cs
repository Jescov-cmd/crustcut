using System.IO;
using FluentAssertions;
using PrimeOSTuner.Win.Xbox;
using Xunit;

namespace PrimeOSTuner.Tests.Xbox;

public class XboxLibraryScannerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"xbox-{Guid.NewGuid():N}");

    public XboxLibraryScannerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    // Real shape of a MicrosoftGame.config from C:\XboxGames\...\Content.
    private const string SampleConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <Game configVersion="1">
          <ExecutableList>
            <Executable Name="DiggingGame.exe" Id="AppShipping" TargetDeviceFamily="PC" />
          </ExecutableList>
          <GameDVRSystemComponent>true</GameDVRSystemComponent>
        </Game>
        """;

    [Fact]
    public void ParseExecutableName_reads_the_executable_from_config()
    {
        var cfg = Path.Combine(_dir, "MicrosoftGame.config");
        File.WriteAllText(cfg, SampleConfig);

        XboxLibraryScanner.ParseExecutableName(cfg).Should().Be("DiggingGame.exe");
    }

    [Fact]
    public void ParseExecutableName_returns_null_for_missing_or_garbage()
    {
        XboxLibraryScanner.ParseExecutableName(Path.Combine(_dir, "nope.config")).Should().BeNull();

        var bad = Path.Combine(_dir, "bad.config");
        File.WriteAllText(bad, "<<<not xml");
        XboxLibraryScanner.ParseExecutableName(bad).Should().BeNull();
    }

    [Fact]
    public void ResolveExecutable_prefers_the_config_named_exe_when_present()
    {
        File.WriteAllText(Path.Combine(_dir, "MicrosoftGame.config"), SampleConfig);
        File.WriteAllText(Path.Combine(_dir, "DiggingGame.exe"), "game");
        File.WriteAllText(Path.Combine(_dir, "UnityCrashHandler64.exe"), "noise");

        XboxLibraryScanner.ResolveExecutable(_dir).Should().Be(Path.Combine(_dir, "DiggingGame.exe"));
    }

    [Fact]
    public void ResolveExecutable_falls_back_to_largest_non_helper_exe_without_config()
    {
        // No config — should skip the crash handler and pick the bigger real exe.
        File.WriteAllText(Path.Combine(_dir, "crashpad_handler.exe"), new string('x', 10));
        File.WriteAllText(Path.Combine(_dir, "TheGame.exe"), new string('x', 5000));

        XboxLibraryScanner.ResolveExecutable(_dir).Should().Be(Path.Combine(_dir, "TheGame.exe"));
    }

    [Fact]
    public void ResolveExecutable_finds_config_exe_in_a_subfolder()
    {
        File.WriteAllText(Path.Combine(_dir, "MicrosoftGame.config"), SampleConfig);
        var sub = Path.Combine(_dir, "Binaries");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "DiggingGame.exe"), "game");

        XboxLibraryScanner.ResolveExecutable(_dir).Should().Be(Path.Combine(sub, "DiggingGame.exe"));
    }

    [Fact]
    public void ScanInstalledGames_never_throws()
    {
        // Real-drive scan: must be safe regardless of what's installed.
        var act = () => new XboxLibraryScanner().ScanInstalledGames();
        act.Should().NotThrow();
    }
}
