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
    public async Task Apply_writes_CPMINCORES_100_to_every_scheme()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.SetValueIndexOnAllSchemes(SubGuid, SetGuid, 100))
              .Returns(new Dictionary<Guid, int?> { [Guid.NewGuid()] = 0 });

        var tweak = new CpuCoreParkingTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetValueIndexOnAllSchemes(SubGuid, SetGuid, 100), Times.Once);
        result.UndoData.Should().Contain("PerScheme");
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
    public async Task Revert_restores_a_legacy_int_undo_everywhere()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        (await tweak.RevertAsync("25")).Succeeded.Should().BeTrue();

        client.Verify(c => c.RestoreValueIndexPerScheme(
            SubGuid, SetGuid, It.IsAny<IReadOnlyDictionary<Guid, int?>>(), 25), Times.Once);
    }

    /// <summary>
    /// An undo that "restores" 100 was recorded by an apply that ran while the tweak was
    /// already applied. Reverting to it turns the tweak back ON — the exact "this
    /// optimizer won't disable" bug. Poisoned undo gets the Windows default instead.
    /// </summary>
    [Fact]
    public async Task Revert_treats_an_undo_equal_to_the_on_value_as_poisoned()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        (await tweak.RevertAsync("100")).Succeeded.Should().BeTrue();

        client.Verify(c => c.RestoreValueIndexPerScheme(
            SubGuid, SetGuid, It.IsAny<IReadOnlyDictionary<Guid, int?>>(), 0), Times.Once);
    }

    [Fact]
    public async Task Revert_to_default_works_without_any_undo_data()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        (await tweak.RevertToDefaultAsync()).Succeeded.Should().BeTrue();

        client.Verify(c => c.RestoreValueIndexPerScheme(
            SubGuid, SetGuid, It.IsAny<IReadOnlyDictionary<Guid, int?>>(), 0), Times.Once);
    }
}
