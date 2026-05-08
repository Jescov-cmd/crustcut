using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class SystemResponsivenessTweakTests
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    [Fact]
    public async Task Apply_writes_SystemResponsiveness_to_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "0"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "20"));

        var tweak = new SystemResponsivenessTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "0"), Times.Once);
    }
}
