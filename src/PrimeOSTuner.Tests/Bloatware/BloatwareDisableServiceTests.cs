using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Bloatware;

public class BloatwareDisableServiceTests
{
    private static BloatwareItem Item(string name) => new(
        new BloatwareCatalogEntry(name, name, "preinstalled", SafetyTier.Safe, null),
        BloatwareStatus.Installed,
        $"{name}_1.0_x64",
        null);

    [Fact]
    public async Task DisableAsync_disables_known_services_for_xbox_overlay()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read(It.IsAny<string>())).Returns(new ServiceState(true, "Manual", false));
        var service = new BloatwareDisableService(svc.Object);

        await service.DisableAsync(Item("Microsoft.XboxGamingOverlay"));

        svc.Verify(s => s.SetStartTypeDisabled("XblGameSave"), Times.AtMostOnce);
        svc.Verify(s => s.SetStartTypeDisabled("XboxGipSvc"), Times.AtMostOnce);
    }

    [Fact]
    public async Task DisableAsync_is_no_op_for_unknown_package()
    {
        var svc = new Mock<IServiceClient>();
        var service = new BloatwareDisableService(svc.Object);

        await service.DisableAsync(Item("Some.Unknown.Package"));

        svc.Verify(s => s.SetStartTypeDisabled(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EnableAsync_restores_known_services()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read(It.IsAny<string>())).Returns(new ServiceState(true, "Disabled", false));
        var service = new BloatwareDisableService(svc.Object);

        await service.EnableAsync(Item("Microsoft.XboxGamingOverlay"));

        svc.Verify(s => s.SetStartType("XblGameSave", "Manual"), Times.AtMostOnce);
    }
}
