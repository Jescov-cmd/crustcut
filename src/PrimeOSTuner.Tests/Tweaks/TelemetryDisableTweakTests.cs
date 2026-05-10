using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TelemetryDisableTweakTests
{
    [Fact]
    public async Task ApplyAsync_writes_three_registry_keys_and_disables_diagtrack()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Auto", true));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection", "AllowTelemetry", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "DoNotShowFeedbackNotifications", 1), Times.Once);
        svc.Verify(s => s.Stop("DiagTrack"), Times.Once);
        svc.Verify(s => s.SetStartTypeDisabled("DiagTrack"), Times.Once);
    }

    [Fact]
    public async Task RevertAsync_restores_all_three_backups_and_resets_service()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Auto", true));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        reg.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
        svc.Verify(s => s.SetStartType("DiagTrack", "Auto"), Times.Once);
    }

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_telemetry_is_zero_and_service_disabled()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry")).Returns(0);

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Disabled", false));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
