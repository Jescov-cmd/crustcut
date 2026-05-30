using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class CpuCoreParkingTweakTests
{
    private const string SubGuid = "54533251-82be-4824-96c1-47b60b740d00";
    private const string SetGuid = "0cc5b647-c1df-4637-891a-dec35c318583";

    [Fact]
    public async Task Apply_calls_setacvalueindex_with_SUB_PROCESSOR_CPMINCORES_100()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActiveSchemeSettingIndexFromRegistry(SubGuid, SetGuid)).Returns(0);

        var tweak = new CpuCoreParkingTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 100), Times.Once);
        result.UndoData.Should().Contain("0");
    }

    [Fact]
    public async Task Probe_reads_hidden_setting_from_registry_and_reports_Applied_at_100()
    {
        // Regression: powercfg /query can't return the hidden CPMINCORES setting, so the
        // tile always read "off" after a successful apply. The probe must read the registry.
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActiveSchemeSettingIndexFromRegistry(SubGuid, SetGuid)).Returns(100);

        var tweak = new CpuCoreParkingTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_reports_NotApplied_when_unset_or_not_100()
    {
        var client = new Mock<IPowerPlanClient>();
        client.SetupSequence(c => c.GetActiveSchemeSettingIndexFromRegistry(SubGuid, SetGuid))
              .Returns((int?)null)   // setting at default / not explicitly set
              .Returns(0);           // explicitly set to a non-target value

        var tweak = new CpuCoreParkingTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_previous_index()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        var result = await tweak.RevertAsync("25");

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 25), Times.Once);
    }
}
