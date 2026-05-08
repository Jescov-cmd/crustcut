using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class JunkFileTweakTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"primeos-junk-{Guid.NewGuid()}");

    public JunkFileTweakTests() => Directory.CreateDirectory(_tempRoot);
    public void Dispose() { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); }

    [Fact]
    public async Task Apply_deletes_files_in_target_dirs_and_returns_freed_bytes()
    {
        var sub = Path.Combine(_tempRoot, "Temp");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "a.tmp"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(sub, "b.tmp"), new byte[2048]);

        var tweak = new JunkFileTweak(new[] { sub });

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("3072");
        Directory.GetFiles(sub).Should().BeEmpty();
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_junk_present()
    {
        var sub = Path.Combine(_tempRoot, "Temp");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "a.tmp"), new byte[100]);

        var tweak = new JunkFileTweak(new[] { sub });

        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
