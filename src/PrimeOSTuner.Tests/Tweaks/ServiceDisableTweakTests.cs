using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class ServiceDisableTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_service_is_disabled_and_stopped()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Disabled", false));
        var tweak = new ServiceDisableTweak(
            id: "core.sysmain-disable",
            displayName: "Disable SysMain",
            description: "x",
            category: "system",
            serviceName: "SysMain",
            riskNote: null,
            client: svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_Unknown_when_service_does_not_exist()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("Bogus")).Returns(new ServiceState(false, "Unknown", false));
        var tweak = new ServiceDisableTweak(
            "x", "x", "x", "system", "Bogus", null, svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Unknown);
    }

    [Fact]
    public async Task ApplyAsync_stops_service_and_disables_start_type()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Auto", true));
        var tweak = new ServiceDisableTweak(
            "core.sysmain-disable", "x", "x", "system", "SysMain", null, svc.Object);

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        svc.Verify(s => s.Stop("SysMain"), Times.Once);
        svc.Verify(s => s.SetStartTypeDisabled("SysMain"), Times.Once);
        result.UndoData.Should().Contain("Auto");
    }

    [Fact]
    public async Task RevertAsync_restores_previous_start_type()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Auto", true));
        var tweak = new ServiceDisableTweak(
            "core.sysmain-disable", "x", "x", "system", "SysMain", null, svc.Object);
        var apply = await tweak.ApplyAsync();

        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        svc.Verify(s => s.SetStartType("SysMain", "Auto"), Times.Once);
    }
}
