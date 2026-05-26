using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class SnappyUiTweakTests
{
    private const string SubKey = @"Control Panel\Desktop";

    [Fact]
    public async Task Apply_writes_all_four_values()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MenuShowDelay", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MenuShowDelay", "400"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "HungAppTimeout", "1000"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "HungAppTimeout", "5000"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "WaitToKillAppTimeout", "5000"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "WaitToKillAppTimeout", "20000"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AutoEndTasks", "1"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AutoEndTasks", "0"));

        var tweak = new SnappyUiTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("MenuShowDelay");
        result.UndoData.Should().Contain("HungAppTimeout");
        result.UndoData.Should().Contain("WaitToKillAppTimeout");
        result.UndoData.Should().Contain("AutoEndTasks");
    }

    [Fact]
    public async Task Probe_returns_Applied_only_when_every_target_matches()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MenuShowDelay")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "HungAppTimeout")).Returns("1000");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "WaitToKillAppTimeout")).Returns("5000");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "AutoEndTasks")).Returns("1");

        var tweak = new SnappyUiTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_the_menu_delay_is_still_default()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MenuShowDelay")).Returns("400");

        var tweak = new SnappyUiTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_every_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, It.IsAny<string>(), It.IsAny<string>()))
                .Returns((RegistryHive h, string k, string n, string v) => new RegistryBackup(h, k, n, "prev"));

        var tweak = new SnappyUiTweak(registry.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(4));
    }
}
