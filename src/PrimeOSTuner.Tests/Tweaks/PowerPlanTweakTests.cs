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
        var tweak = new PowerPlanTweak(client.Object);

        var result = await tweak.RevertAsync(BalancedGuid.ToString("D"));

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActivePlan(BalancedGuid), Times.Once);
    }
}
