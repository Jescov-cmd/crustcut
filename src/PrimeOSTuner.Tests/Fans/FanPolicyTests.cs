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
            FanPolicy.Evaluate(mode, 90).Should().Be(100);
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
        // Silent: (60, 26) -> (70, 34). Midpoint (65deg) => 30%.
        FanPolicy.Evaluate(FanMode.Silent, 65).Should().BeApproximately(30, 0.01);
    }

    private const double CoolC = 50;   // comfortably below every temperature band

    [Theory]
    [InlineData(3, FanMode.Silent)]        // idle desktop
    [InlineData(15, FanMode.Silent)]       // a light 2D game: stays quiet
    [InlineData(40, FanMode.Balanced)]     // real work
    [InlineData(85, FanMode.Performance)]  // pinned
    public void Auto_follows_load_not_whether_something_is_called_a_game(double load, FanMode expected)
    {
        // Starting from Silent each time, so these are upward transitions (hysteresis only
        // resists stepping DOWN).
        FanPolicy.ResolveMode(FanMode.Auto, load, CoolC, FanMode.Silent).Should().Be(expected);
    }

    [Theory]
    [InlineData(60, FanMode.Silent)]        // cool: load decides
    [InlineData(78, FanMode.Silent)]        // a boosting CPU's normal working temperature
    [InlineData(84, FanMode.Balanced)]      // hotter than design target: start helping
    [InlineData(89, FanMode.Performance)]   // genuinely getting away: full cooling
    public void Auto_escalates_on_heat_even_when_the_machine_is_idle(double tempC, FanMode expected)
    {
        // The safety half of Auto: a warm room, dusty filters or a background thermal
        // event can cook a machine that is doing almost nothing.
        //
        // 78°C deliberately does NOT escalate. Modern CPUs boost until they reach their
        // thermal target, so the high 70s is the chip working as designed — and cooling it
        // harder just buys more boost at the same temperature. Escalating there pinned the
        // fans at full speed on an idle desktop and never let them back down.
        FanPolicy.ResolveMode(FanMode.Auto, loadPercent: 2, tempC, FanMode.Silent)
            .Should().Be(expected);
    }

    [Fact]
    public void Auto_obeys_whichever_signal_demands_more_cooling()
    {
        // Busy but cool, and idle but hot, both land on Performance.
        FanPolicy.ResolveMode(FanMode.Auto, 90, CoolC, FanMode.Silent).Should().Be(FanMode.Performance);
        FanPolicy.ResolveMode(FanMode.Auto, 2, 89, FanMode.Silent).Should().Be(FanMode.Performance);
    }

    [Fact]
    public void Auto_holds_its_band_until_BOTH_signals_clear_the_hysteresis_margin()
    {
        // Load has eased but the machine is still hot: keep cooling.
        FanPolicy.ResolveMode(FanMode.Auto, 40, 85, FanMode.Performance).Should().Be(FanMode.Performance);
        // Cool but still busy: keep cooling.
        FanPolicy.ResolveMode(FanMode.Auto, 60, CoolC, FanMode.Performance).Should().Be(FanMode.Performance);
        // Both eased: step down.
        FanPolicy.ResolveMode(FanMode.Auto, 40, CoolC, FanMode.Performance).Should().Be(FanMode.Balanced);

        FanPolicy.ResolveMode(FanMode.Auto, 10, 79, FanMode.Balanced).Should().Be(FanMode.Balanced);
        FanPolicy.ResolveMode(FanMode.Auto, 10, CoolC, FanMode.Balanced).Should().Be(FanMode.Silent);
    }

    [Fact]
    public void Explicitly_chosen_modes_ignore_load_and_temperature()
    {
        foreach (var mode in new[] { FanMode.Silent, FanMode.Balanced, FanMode.Performance })
        {
            FanPolicy.ResolveMode(mode, 95, 82, FanMode.Silent).Should().Be(mode);
            FanPolicy.ResolveMode(mode, 0, 40, FanMode.Performance).Should().Be(mode);
        }
    }

    [Fact]
    public void Silent_chosen_manually_still_hits_the_failsafe_when_it_matters()
    {
        // Manual Silent is a preference, not a suicide pact: the curve itself still ramps,
        // and the 90°C failsafe overrides everything.
        FanPolicy.Evaluate(FanMode.Silent, 89).Should().BeGreaterThan(
            FanPolicy.Evaluate(FanMode.Silent, 70));
        FanPolicy.Evaluate(FanMode.Silent, 90).Should().Be(100);
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
    public void Silent_is_quiet_when_actually_cool_but_still_cools_a_boosting_cpu()
    {
        // Silence is bought with airflow, never with CPU speed. Below 60degC there is
        // nothing to cool, so Silent stays genuinely quiet.
        FanPolicy.Evaluate(FanMode.Silent, 50).Should().BeLessThan(26);
        FanPolicy.Evaluate(FanMode.Silent, 60).Should().BeLessThan(30);

        // But a CPU allowed to boost deliberately sits in the 70s doing very little, and
        // an earlier version of this curve coasted there (28% at 72degC) because it had
        // been tuned on a machine with boost switched off. Even the quiet curve has to
        // move real air once the chip is actually hot.
        FanPolicy.Evaluate(FanMode.Silent, 70).Should().BeGreaterThan(30);
        FanPolicy.Evaluate(FanMode.Silent, 80).Should().BeGreaterThan(50);
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
