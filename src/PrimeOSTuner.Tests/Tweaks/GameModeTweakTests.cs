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

    [Fact]
    public async Task Apply_writes_AllowAutoGameMode_and_AutoGameModeEnabled_to_1()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "1"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "0"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", "1"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", null));

        var tweak = new GameModeTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "1"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", "1"), Times.Once);
    }

    [Fact]
    public async Task Probe_returns_Applied_when_both_values_equal_one()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode")).Returns("1");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled")).Returns("1");

        var tweak = new GameModeTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
