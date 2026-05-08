using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class NetworkThrottlingIndexTweakTests
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    [Fact]
    public async Task Apply_writes_NetworkThrottlingIndex_to_max_uint()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0xffffffff"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0x0000000a"));

        var tweak = new NetworkThrottlingIndexTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0xffffffff"), Times.Once);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new NetworkThrottlingIndexTweak(Mock.Of<IRegistryClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }
}
