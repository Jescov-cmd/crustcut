using FluentAssertions;
using PrimeOSTuner.Core.Fans;
using Xunit;

namespace PrimeOSTuner.Tests.Fans;

public class ThermalGovernorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime After(double seconds) => T0.AddSeconds(seconds);

    [Fact]
    public void Reduces_only_after_sustained_heat_not_a_spike()
    {
        var g = new ThermalGovernor();

        // A boost spike: one hot reading, back to warm. Ryzen does this constantly.
        g.Update(88, T0).Should().Be(GovernorAction.None);
        g.Update(76, After(2)).Should().Be(GovernorAction.None);
        g.Reduced.Should().BeFalse();

        // Genuine heat soak: hot the whole window.
        g.Update(87, After(10)).Should().Be(GovernorAction.None);
        g.Update(88, After(20)).Should().Be(GovernorAction.None);   // timer restarted at 10s
        g.Update(89, After(31)).Should().Be(GovernorAction.ReduceBoost);
        g.Reduced.Should().BeTrue();
    }

    [Fact]
    public void Restores_only_after_a_sustained_cool_period()
    {
        var g = new ThermalGovernor();
        g.Update(90, T0);
        g.Update(90, After(25)).Should().Be(GovernorAction.ReduceBoost);

        // Dips below the restore line but pops back up — no restore.
        g.Update(70, After(30)).Should().Be(GovernorAction.None);
        g.Update(80, After(40)).Should().Be(GovernorAction.None);

        // Properly cool for the full minute.
        g.Update(68, After(50)).Should().Be(GovernorAction.None);
        g.Update(65, After(111)).Should().Be(GovernorAction.RestoreBoost);
        g.Reduced.Should().BeFalse();
    }

    [Fact]
    public void The_warm_band_between_the_edges_holds_state()
    {
        var g = new ThermalGovernor();
        g.Update(90, T0);
        g.Update(90, After(25)).Should().Be(GovernorAction.ReduceBoost);

        // 72-85 is hysteresis: hovering there must not flap boost either way.
        foreach (var s in new[] { 30.0, 90, 300, 900 })
            g.Update(78, After(s)).Should().Be(GovernorAction.None);
        g.Reduced.Should().BeTrue();
    }

    [Fact]
    public void Never_reduces_twice_and_never_restores_when_not_reduced()
    {
        var g = new ThermalGovernor();
        g.Update(65, T0).Should().Be(GovernorAction.None);
        g.Update(65, After(120)).Should().Be(GovernorAction.None);   // cool but nothing to restore

        g.Update(90, After(130));
        g.Update(90, After(155)).Should().Be(GovernorAction.ReduceBoost);
        g.Update(95, After(200)).Should().Be(GovernorAction.None);   // hotter still — already reduced
    }

    [Fact]
    public void NotifyRestored_resyncs_after_an_external_restore()
    {
        var g = new ThermalGovernor();
        g.Update(90, T0);
        g.Update(90, After(25)).Should().Be(GovernorAction.ReduceBoost);

        g.NotifyRestored();   // e.g. the user switched the governor off
        g.Reduced.Should().BeFalse();

        // Heat returns: it must re-earn the reduce with a fresh sustained window.
        g.Update(90, After(60)).Should().Be(GovernorAction.None);
        g.Update(90, After(81)).Should().Be(GovernorAction.ReduceBoost);
    }
}
