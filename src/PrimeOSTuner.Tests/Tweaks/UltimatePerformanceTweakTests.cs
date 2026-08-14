using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class UltimatePerformanceTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_powercfg_list_contains_ultimate_guid()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61  (Ultimate Performance) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_NotApplied_when_ultimate_not_in_list()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task ApplyAsync_duplicates_when_no_ultimate_scheme_exists()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.ListPlans()).Returns(new[] { new PowerPlan(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced") });
        ppc.Setup(p => p.RunPowercfg("/duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61"))
           .Returns("Power Scheme GUID: 11111111-1111-1111-1111-111111111111  (Ultimate Performance)");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task ApplyAsync_is_idempotent_and_does_not_duplicate_when_one_exists()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.ListPlans()).Returns(new[]
        {
            new PowerPlan(Guid.Parse("39d2f02b-aaa6-4dc5-a651-2e9d999950c1"), "Ultimate Performance"),
        });
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        // Must reuse the existing scheme — never run /duplicatescheme again.
        ppc.Verify(p => p.RunPowercfg(It.Is<string>(s => s.StartsWith("/duplicatescheme"))), Times.Never);
    }

    /// <summary>
    /// The normal case IS "the user is on Ultimate Performance" — enabling the plan is
    /// pointless without switching to it. Windows refuses to delete the active plan, and
    /// the old code silently skipped it: the toggle claimed success, removed nothing, and
    /// the user was told to go fix it in Control Panel by hand. Now revert steps the
    /// machine onto Balanced first, then removes every Ultimate scheme.
    /// </summary>
    [Fact]
    public async Task RevertAsync_switches_to_Balanced_first_so_the_active_scheme_can_be_deleted()
    {
        var balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
        var dup1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var activeUltimate = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.GetActivePlan()).Returns(new PowerPlan(activeUltimate, "Ultimate Performance"));
        ppc.Setup(p => p.ListPlans()).Returns(new[]
        {
            new PowerPlan(dup1, "Ultimate Performance"),
            new PowerPlan(activeUltimate, "Ultimate Performance"),
            new PowerPlan(balanced, "Balanced"),
        });

        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.RevertAsync("\"ignored-stale-guid\"");

        result.Succeeded.Should().BeTrue();
        ppc.Verify(p => p.SetActivePlan(balanced), Times.Once);
        ppc.Verify(p => p.RunPowercfg($"/delete {dup1:D}"), Times.Once);
        ppc.Verify(p => p.RunPowercfg($"/delete {activeUltimate:D}"), Times.Once);
        result.Message.Should().Contain("Balanced");
    }

    [Fact]
    public async Task RevertAsync_does_not_touch_the_active_plan_when_it_is_not_ultimate()
    {
        var gone = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.GetActivePlan()).Returns(new PowerPlan(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced"));
        ppc.Setup(p => p.ListPlans()).Returns(new[] { new PowerPlan(gone, "Ultimate Performance") });

        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.RevertAsync("\"stale\"")).Succeeded.Should().BeTrue();

        ppc.Verify(p => p.SetActivePlan(It.IsAny<Guid>()), Times.Never);
        ppc.Verify(p => p.RunPowercfg($"/delete {gone:D}"), Times.Once);
    }

    [Fact]
    public async Task RevertAsync_tolerates_a_scheme_that_is_already_gone()
    {
        var gone = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.GetActivePlan()).Returns(new PowerPlan(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced"));
        ppc.Setup(p => p.ListPlans()).Returns(new[] { new PowerPlan(gone, "Ultimate Performance") });
        ppc.Setup(p => p.RunPowercfg($"/delete {gone:D}"))
           .Throws(new InvalidOperationException("The power scheme ... does not exist."));

        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.RevertAsync("\"stale\"");

        result.Succeeded.Should().BeTrue();   // absence == success
    }
}
