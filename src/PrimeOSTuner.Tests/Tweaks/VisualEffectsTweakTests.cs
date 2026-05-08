using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class VisualEffectsTweakTests
{
    [Fact]
    public async Task Apply_writes_VisualFXSetting_to_2_and_returns_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(
            RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting",
            "2"))
        .Returns(new RegistryBackup(
            RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting",
            "0"));

        var tweak = new VisualEffectsTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("VisualFXSetting");
    }
}
