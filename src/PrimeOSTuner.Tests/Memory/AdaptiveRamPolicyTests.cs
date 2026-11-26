using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class AdaptiveRamPolicyTests
{
    private static RamPressureSnapshot Snap(double used, long freeMb = 4096, double faults = 0)
        => new(used, freeMb * 1024L * 1024, faults);

    private static readonly TimeSpan LongAgo = TimeSpan.FromHours(1);

    [Fact]
    public void RecommendedLimits_snaps_to_presets_and_knows_the_catalog()
    {
        RecommendedLimits.SnapToPreset(400).Should().Be(512);
        RecommendedLimits.SnapToPreset(512).Should().Be(512);
        RecommendedLimits.SnapToPreset(1500).Should().Be(2048);
        RecommendedLimits.SnapToPreset(9000).Should().BeNull();   // beyond every preset
        RecommendedLimits.KnownAppLimitsMb["steamwebhelper"].Should().Be(512);
        RecommendedLimits.KnownAppLimitsMb.ContainsKey("Crustcut").Should().BeFalse();
    }

    [Fact]
    public void Recommended_limits_keep_generous_headroom()
    {
        // A cap sitting just above real usage makes Windows evict constantly. Anything
        // measured must land on a preset comfortably above it.
        RecommendedLimits.SnapToPreset((long)(400 * 1.5)).Should().Be(1024);
        RecommendedLimits.SnapToPreset((long)(300 * 1.5)).Should().Be(512);
    }

    [Fact]
    public void Comfortable_memory_does_nothing()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(50), LongAgo);
        d.Clean.Should().BeFalse();
    }

    [Fact]
    public void Paging_storm_backs_off_even_under_critical_pressure()
    {
        // The thrash guard outranks everything: trimming during a fault storm makes it worse.
        var d = AdaptiveRamPolicy.Decide(Snap(95, freeMb: 300, faults: 900), LongAgo);
        d.Clean.Should().BeFalse();
        d.Reason.Should().Contain("paging storm");
    }

    [Fact]
    public void Elevated_pressure_cleans_after_cooldown()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(75), LongAgo);
        d.Clean.Should().BeTrue();
    }

    [Fact]
    public void Cooldown_prevents_back_to_back_cleans()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(2));
        d.Clean.Should().BeFalse();
    }

    [Fact]
    public void Starved_free_memory_counts_as_critical_regardless_of_percent()
    {
        // 4-minute critical cooldown met, 6-minute high cooldown not: only the critical
        // band cleans here, and free < 800 MB is what puts an 88% machine in it.
        var starved = AdaptiveRamPolicy.Decide(Snap(88, freeMb: 400), TimeSpan.FromMinutes(5));
        starved.Clean.Should().BeTrue();
        starved.Reason.Should().StartWith("critical");

        var roomy = AdaptiveRamPolicy.Decide(Snap(88, freeMb: 4096), TimeSpan.FromMinutes(5));
        roomy.Clean.Should().BeFalse();
    }

    [Fact]
    public void Cache_size_never_enters_the_decision()
    {
        // The standby cache used to trigger an automatic purge whenever it looked large
        // while free memory looked starved. That purge is gone — the policy has no cache
        // input at all, and its only verdict is whether to trim idle working sets.
        var d = AdaptiveRamPolicy.Decide(Snap(88, freeMb: 500), TimeSpan.FromMinutes(2));
        d.Clean.Should().BeFalse();
        d.Reason.Should().Contain("cooling down");
        d.GetType().GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "Clean", "Reason" });
    }

    [Fact]
    public void Game_running_halves_cooldown()
    {
        // 3.5 minutes since last clean: too soon on desktop (6m), fine in game (3m).
        var desktop = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(3.5));
        desktop.Clean.Should().BeFalse();

        var inGame = AdaptiveRamPolicy.Decide(
            Snap(88) with { GameRunning = true }, TimeSpan.FromMinutes(3.5));
        inGame.Clean.Should().BeTrue();
        inGame.Reason.Should().Contain("in game");
    }

    [Fact]
    public void Fast_rising_trend_bumps_the_pressure_level()
    {
        // 78% is normally below the 'high' band; climbing 6pp/min treats it as high.
        var stable = AdaptiveRamPolicy.Decide(Snap(78), TimeSpan.FromMinutes(7));
        var rising = AdaptiveRamPolicy.Decide(
            Snap(78) with { TrendPercentPerMin = 6 }, TimeSpan.FromMinutes(7));

        stable.Clean.Should().BeFalse();   // elevated band wants a 10m cooldown
        rising.Clean.Should().BeTrue();    // bumped to high: 6m cooldown already met
        rising.Reason.Should().Contain("rising fast");
    }

    [Fact]
    public void Consecutive_ineffective_cleans_stretch_the_cooldown()
    {
        // High pressure, 8 minutes since last clean: normally due (6m) — but after two
        // no-yield cleans the wait stretches to 18m, so it holds off.
        var fresh = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(8));
        fresh.Clean.Should().BeTrue();

        var futile = AdaptiveRamPolicy.Decide(
            Snap(88), TimeSpan.FromMinutes(8), consecutiveIneffectiveCleans: 2);
        futile.Clean.Should().BeFalse();
        futile.Reason.Should().Contain("cooling down");
    }
}
