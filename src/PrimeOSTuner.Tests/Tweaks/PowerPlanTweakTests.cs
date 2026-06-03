using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class PowerPlanTweakTests
{
    private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid UltimateGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    [Fact]
    public async Task Apply_switches_to_ultimate_and_returns_previous_guid()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActivePlan()).Returns(new PowerPlan(BalancedGuid, "Balanced"));
        client.Setup(c => c.EnsureUltimatePerformancePlan()).Returns(UltimateGuid);

        var tweak = new PowerPlanTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain(BalancedGuid.ToString());
        client.Verify(c => c.SetActivePlan(UltimateGuid), Times.Once);
    }

    [Fact]
    public async Task Revert_sets_active_plan_back_to_undo_guid()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.ListPlans()).Returns(new[] { new PowerPlan(BalancedGuid, "Balanced") });
        var tweak = new PowerPlanTweak(client.Object);

        var result = await tweak.RevertAsync(BalancedGuid.ToString("D"));

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActivePlan(BalancedGuid), Times.Once);
    }

    // Regression: the saved "previous" plan can be deleted later (by the user or a Windows
    // update). powercfg /setactive on a missing GUID throws "Invalid Parameters", which made
    // turning the tweak OFF impossible — the revert threw, an error popup showed, and the
    // toggle snapped back on. Revert must fall back to a real plan (Balanced) instead.
    [Fact]
    public async Task Revert_falls_back_to_Balanced_when_the_saved_plan_no_longer_exists()
    {
        var missing = Guid.Parse("3be17db9-98e0-4304-95ca-f7131a163c0d");
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.ListPlans()).Returns(new[]
        {
            new PowerPlan(BalancedGuid, "Balanced"),
            new PowerPlan(UltimateGuid, "Ultimate Performance"),
        });

        var tweak = new PowerPlanTweak(client.Object);
        var result = await tweak.RevertAsync(missing.ToString("D"));

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActivePlan(BalancedGuid), Times.Once);
        client.Verify(c => c.SetActivePlan(missing), Times.Never);
    }

    // Regression: Apply activates a DUPLICATED Ultimate Performance scheme whose GUID is
    // NOT the template GUID. The probe must match by NAME, or the tile always reads "off".
    [Fact]
    public async Task Probe_reports_Applied_when_active_scheme_is_named_Ultimate_Performance_even_with_a_different_guid()
    {
        var duplicatedUltimate = Guid.Parse("9d3904d8-f70d-4c4f-8e43-3e828f88ac33");
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActivePlan()).Returns(new PowerPlan(duplicatedUltimate, "Ultimate Performance"));

        var tweak = new PowerPlanTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_reports_NotApplied_when_active_scheme_is_Balanced()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActivePlan()).Returns(new PowerPlan(BalancedGuid, "Balanced"));

        var tweak = new PowerPlanTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
