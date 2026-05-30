using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class HwGpuSchedulingTweakTests
{
    private const string SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    // Regression: HwSchMode is a REG_DWORD. Writing it as a string left a value the
    // GPU driver ignores (HAGS never actually turned on, yet the tile said "applied").
    [Fact]
    public async Task Apply_writes_HwSchMode_dword_2_under_HKLM()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, SubKey, "HwSchMode", 2))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "HwSchMode", null, 1));

        var tweak = new HwGpuSchedulingTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteDword(RegistryHive.LocalMachine, SubKey, "HwSchMode", 2), Times.Once);
        registry.Verify(r => r.WriteString(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new HwGpuSchedulingTweak(Mock.Of<IRegistryClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }

    [Fact]
    public async Task Probe_returns_Applied_only_when_HwSchMode_is_2()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, SubKey, "HwSchMode")).Returns(2);
        var tweak = new HwGpuSchedulingTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
