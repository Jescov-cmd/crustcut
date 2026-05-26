using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class StickyKeysTweakTests
{
    private const string Sticky = @"Control Panel\Accessibility\StickyKeys";
    private const string Filter = @"Control Panel\Accessibility\Keyboard Response";
    private const string Toggle = @"Control Panel\Accessibility\ToggleKeys";

    [Fact]
    public async Task Apply_writes_shortcut_off_flags_for_all_three_accessibility_features()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, Sticky, "Flags", "506"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, Sticky, "Flags", "510"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, Filter, "Flags", "122"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, Filter, "Flags", "126"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, Toggle, "Flags", "58"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, Toggle, "Flags", "62"));

        var tweak = new StickyKeysTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("StickyKeys");
        result.UndoData.Should().Contain("Keyboard Response");
        result.UndoData.Should().Contain("ToggleKeys");
    }

    [Fact]
    public async Task Probe_returns_Applied_only_when_all_three_flag_values_match()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, Sticky, "Flags")).Returns("506");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, Filter, "Flags")).Returns("122");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, Toggle, "Flags")).Returns("58");

        var tweak = new StickyKeysTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_any_flag_is_still_default()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, Sticky, "Flags")).Returns("510");

        var tweak = new StickyKeysTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
