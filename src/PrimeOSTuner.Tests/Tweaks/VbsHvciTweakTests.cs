using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class VbsHvciTweakTests
{
    private const string DeviceGuard = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string Hvci = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";

    [Fact]
    public async Task Apply_writes_zeroes_to_both_VBS_and_HVCI_keys()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, DeviceGuard, "EnableVirtualizationBasedSecurity", 0))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, DeviceGuard, "EnableVirtualizationBasedSecurity", null, 1, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, Hvci, "Enabled", 0))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, Hvci, "Enabled", null, 1, RegistryValueKind.DWord));

        var tweak = new VbsHvciTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("EnableVirtualizationBasedSecurity");
        result.UndoData.Should().Contain("Enabled");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_both_values_are_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, DeviceGuard, "EnableVirtualizationBasedSecurity")).Returns(0);
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, Hvci, "Enabled")).Returns(0);

        var tweak = new VbsHvciTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task IsDestructive_is_true_because_the_tweak_weakens_OS_security()
    {
        var tweak = new VbsHvciTweak(new Mock<IRegistryClient>().Object);
        await Task.CompletedTask;
        tweak.IsDestructive.Should().BeTrue();
    }
}
