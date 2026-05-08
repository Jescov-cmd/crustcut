using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class PowerPlanClientTests
{
    [Fact]
    public void ListPlans_includes_at_least_balanced()
    {
        var client = new PowerPlanClient();
        var plans = client.ListPlans();

        plans.Should().Contain(p => p.Name.Equals("Balanced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetActivePlan_returns_a_plan_from_the_listed_set()
    {
        var client = new PowerPlanClient();
        var plans = client.ListPlans();
        var active = client.GetActivePlan();

        plans.Should().Contain(p => p.Guid == active.Guid);
    }
}
