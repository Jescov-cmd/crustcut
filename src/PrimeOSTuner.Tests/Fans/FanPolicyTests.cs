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
        // Silent: (60, 24) -> (72, 28). Midpoint (66deg) => 26%.
        FanPolicy.Evaluate(FanMode.Silent, 66).Should().BeApproximately(26, 0.01);
    }

    [Theory]
    [InlineData(3, FanMode.Silent)]        // idle desktop
    [InlineData(15, FanMode.Silent)]       // a light 2D game: stays quiet
    [InlineData(40, FanMode.Balanced)]     // real work
    [InlineData(85, FanMode.Performance)]  // pinned
    public void Auto_follows_load_not_whether_something_is_called_a_game(double load, FanMode expected)
    {
        // Starting from Silent each time, so these are upward transitions (hysteresis only
        // resists stepping DOWN).
        FanPolicy.ResolveMode(FanMode.Auto, load, FanMode.Silent).Should().Be(expected);
    }

    [Fact]
    public void Auto_holds_its_band_until_load_clears_the_hysteresis_margin()
    {
        // Sitting just under the Performance edge must NOT drop back immediately.
        FanPolicy.ResolveMode(FanMode.Auto, 60, FanMode.Performance).Should().Be(FanMode.Performance);
        // Well clear of it, it steps down.
        FanPolicy.ResolveMode(FanMode.Auto, 40, FanMode.Performance).Should().Be(FanMode.Balanced);

        FanPolicy.ResolveMode(FanMode.Auto, 20, FanMode.Balanced).Should().Be(FanMode.Balanced);
        FanPolicy.ResolveMode(FanMode.Auto, 10, FanMode.Balanced).Should().Be(FanMode.Silent);
    }

    [Fact]
    public void Explicitly_chosen_modes_ignore_load_entirely()
    {
        foreach (var mode in new[] { FanMode.Silent, FanMode.Balanced, FanMode.Performance })
        {
            FanPolicy.ResolveMode(mode, 95, FanMode.Silent).Should().Be(mode);
            FanPolicy.ResolveMode(mode, 0, FanMode.Performance).Should().Be(mode);
        }
    }

    [Fact]
    public void Uncalibrated_default_is_safer_than_the_tuned_curve_floor()
    {
        // A machine we have never measured must not inherit one PC's fan minimum.
        FanPolicy.UncalibratedMinDutyPercent.Should().BeGreaterThan(FanPolicy.MinDutyPercent);
        FanPolicy.HardMinDutyPercent.Should().BeLessThan(FanPolicy.MinDutyPercent);
    }

    [Fact]
    public void Floor_stays_above_the_measured_stall_point()
    {
        // The user's case fan stalled at 18% duty; the floor must keep real margin.
        FanPolicy.MinDutyPercent.Should().BeGreaterThan(19);
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
