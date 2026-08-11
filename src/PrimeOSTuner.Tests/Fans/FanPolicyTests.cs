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
        // Silent: (60, 32) -> (72, 38). Halfway (66deg) => 35%.
        FanPolicy.Evaluate(FanMode.Silent, 66).Should().BeApproximately(35, 0.01);
    }

    [Fact]
    public void Silent_stays_near_idle_duty_through_ryzens_normal_warm_band()
    {
        // A Ryzen 7700 lives at 65-77degC; Silent must not ramp inside that band.
        FanPolicy.Evaluate(FanMode.Silent, 70).Should().BeLessThan(40);
        FanPolicy.Evaluate(FanMode.Silent, 76).Should().BeLessThan(48);
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
