using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class VisualEffectsTweakTests
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string WindowMetrics = @"Control Panel\Desktop\WindowMetrics";

    [Fact]
    public async Task Apply_writes_all_five_values_and_returns_combined_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, Personalize, "EnableTransparency", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, Personalize, "EnableTransparency", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewAlphaSelect", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewAlphaSelect", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewShadow", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewShadow", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate", "1"));

        var tweak = new VisualEffectsTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("EnableTransparency");
        result.UndoData.Should().Contain("TaskbarAnimations");
        result.UndoData.Should().Contain("MinAnimate");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_transparency_taskbar_and_minanimate_are_off()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, Personalize, "EnableTransparency")).Returns(0);
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations")).Returns(0);
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate")).Returns("0");

        var tweak = new VisualEffectsTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_any_indicator_is_still_on()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, Personalize, "EnableTransparency")).Returns(1);
        registry.Setup(r => r.ReadDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations")).Returns(0);
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate")).Returns("0");

        var tweak = new VisualEffectsTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_every_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, Personalize, "EnableTransparency", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, Personalize, "EnableTransparency", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAnimations", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewAlphaSelect", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewAlphaSelect", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewShadow", 0))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, ExplorerAdvanced, "ListviewShadow", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, WindowMetrics, "MinAnimate", "1"));

        var tweak = new VisualEffectsTweak(registry.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(5));
    }
}
