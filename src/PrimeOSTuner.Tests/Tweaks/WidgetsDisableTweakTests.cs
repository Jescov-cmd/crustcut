using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class WidgetsDisableTweakTests
{
    private const string MachinePolicy = @"SOFTWARE\Policies\Microsoft\Dsh";
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    [Fact]
    public async Task Apply_writes_both_news_and_widgets_keys()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests", 0))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa", null, 1, RegistryValueKind.DWord));

        var tweak = new WidgetsDisableTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("AllowNewsAndInterests");
        result.UndoData.Should().Contain("TaskbarDa");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_both_values_are_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests")).Returns(0);
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa")).Returns(0);

        var tweak = new WidgetsDisableTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_either_value_is_missing_or_enabled()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests")).Returns((int?)null);
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa")).Returns(0);

        var tweak = new WidgetsDisableTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_both_backups()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests", 0))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, MachinePolicy, "AllowNewsAndInterests", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa", null, 1, RegistryValueKind.DWord));

        var tweak = new WidgetsDisableTweak(registry.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(2));
    }
}
