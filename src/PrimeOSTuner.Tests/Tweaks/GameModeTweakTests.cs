using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class GameModeTweakTests
{
    private const string SubKey = @"Software\Microsoft\GameBar";

    // Regression: these are REG_DWORD values. Writing/reading them as strings meant
    // Windows' Game Bar overwrote them back to DWORD 0 and the probe (ReadString on a
    // DWORD => null) reported "not applied" on every reboot. Must use DWORD.
    [Fact]
    public async Task Apply_writes_AllowAutoGameMode_and_AutoGameModeEnabled_to_dword_1()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", 1))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", null, 0));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", 1))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", null));

        var tweak = new GameModeTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteDword(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", 1), Times.Once);
        registry.Verify(r => r.WriteDword(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", 1), Times.Once);
        // Must NOT fall back to a string write — that's the bug we're guarding against.
        registry.Verify(r => r.WriteString(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Probe_returns_Applied_when_both_dword_values_equal_one()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode")).Returns(1);
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled")).Returns(1);

        var tweak = new GameModeTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_dword_is_zero()
    {
        // This is the exact post-reboot state we saw on the user's machine.
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, SubKey, It.IsAny<string>())).Returns(0);

        var tweak = new GameModeTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
