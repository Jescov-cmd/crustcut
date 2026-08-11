using FluentAssertions;
using PrimeOSTuner.Core.Fans;
using Xunit;

namespace PrimeOSTuner.Tests.Fans;

public class FanPolicyTests
{
    [Fact]
    public void Failsafe_temperature_forces_full_speed_in_every_mode()
    {
        foreach (var mode in new[] { FanMode.Silent, FanMode.Balanced, FanMode.Performance })
        {
            FanPolicy.Evaluate(mode, 85).Should().Be(100);
            FanPolicy.Evaluate(mode, 95).Should().Be(100);
        }
    }

    [Fact]
    public void Duty_never_drops_below_the_floor()
    {
        FanPolicy.Evaluate(FanMode.Silent, 20).Should().BeGreaterThanOrEqualTo(FanPolicy.MinDutyPercent);
        FanPolicy.Evaluate(FanMode.Silent, 0).Should().BeGreaterThanOrEqualTo(FanPolicy.MinDutyPercent);
    }

    [Fact]
    public void Interpolates_linearly_between_points()
    {
        // Silent: (60, 40) -> (75, 55). Halfway (67.5deg) => 47.5%.
        FanPolicy.Evaluate(FanMode.Silent, 67.5).Should().BeApproximately(47.5, 0.01);
    }

    [Fact]
    public void Modes_order_by_aggression_at_gaming_temperatures()
    {
        var silent = FanPolicy.Evaluate(FanMode.Silent, 70);
        var balanced = FanPolicy.Evaluate(FanMode.Balanced, 70);
        var performance = FanPolicy.Evaluate(FanMode.Performance, 70);
        silent.Should().BeLessThan(balanced);
        balanced.Should().BeLessThan(performance);
    }

    [Fact]
    public void Silent_mode_is_much_quieter_than_the_users_bios_at_idle_temps()
    {
        // The probe found the BIOS running the CPU fan at 71% duty at 69degC.
        FanPolicy.Evaluate(FanMode.Silent, 69).Should().BeLessThan(55);
    }
}
