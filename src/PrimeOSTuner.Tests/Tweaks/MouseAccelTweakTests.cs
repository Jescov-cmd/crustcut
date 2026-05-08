using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class MouseAccelTweakTests
{
    private const string SubKey = @"Control Panel\Mouse";

    [Fact]
    public async Task Apply_writes_three_values_and_returns_combined_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "1"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "6"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "10"));

        var tweak = new MouseAccelTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("MouseSpeed");
        result.UndoData.Should().Contain("MouseThreshold1");
        result.UndoData.Should().Contain("MouseThreshold2");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_all_three_values_are_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2")).Returns("0");

        var tweak = new MouseAccelTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_any_value_is_nonzero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed")).Returns("1");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2")).Returns("0");

        var tweak = new MouseAccelTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_all_three_backups()
    {
        var registry = new Mock<IRegistryClient>();
        var tweak = new MouseAccelTweak(registry.Object);

        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "1"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "6"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "10"));

        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
    }
}
