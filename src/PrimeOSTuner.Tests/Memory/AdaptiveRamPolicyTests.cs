using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class AdaptiveRamPolicyTests
{
    private static RamPressureSnapshot Snap(
        double used, long freeMb = 4096, long standbyMb = 500, double faults = 0)
        => new(used, freeMb * 1024L * 1024, standbyMb * 1024L * 1024, faults);

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
    public void Comfortable_memory_does_nothing()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(50), LongAgo, -1);
        d.Clean.Should().BeFalse();
        d.PurgeStandby.Should().BeFalse();
    }

    [Fact]
    public void Paging_storm_backs_off_even_under_critical_pressure()
    {
        // The thrash guard outranks everything: trimming during a fault storm makes it worse.
        var d = AdaptiveRamPolicy.Decide(Snap(95, freeMb: 300, faults: 900), LongAgo, -1);
        d.Clean.Should().BeFalse();
        d.PurgeStandby.Should().BeFalse();
        d.Reason.Should().Contain("paging storm");
    }

    [Fact]
    public void Elevated_pressure_cleans_normal_after_cooldown()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(75), LongAgo, -1);
        d.Clean.Should().BeTrue();
        d.Mode.Should().Be(RamCleanMode.Normal);
    }

    [Fact]
    public void Cooldown_prevents_back_to_back_cleans()
    {
        var d = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(2), -1);
        d.Clean.Should().BeFalse();
    }

    [Fact]
    public void Critical_pressure_with_ineffective_last_clean_escalates_to_deep()
    {
        var d = AdaptiveRamPolicy.Decide(
            Snap(95, freeMb: 400), LongAgo, lastCleanFreedBytes: 50L * 1024 * 1024);
        d.Clean.Should().BeTrue();
        d.Mode.Should().Be(RamCleanMode.Deep);
    }

    [Fact]
    public void Critical_pressure_with_effective_last_clean_stays_normal()
    {
        var d = AdaptiveRamPolicy.Decide(
            Snap(95, freeMb: 400), LongAgo, lastCleanFreedBytes: 600L * 1024 * 1024);
        d.Mode.Should().Be(RamCleanMode.Normal);
    }

    [Fact]
    public void Purge_rides_along_when_cache_hoards_while_free_is_starved()
    {
        var d = AdaptiveRamPolicy.Decide(
            Snap(88, freeMb: 500, standbyMb: 2000), LongAgo, -1);
        d.PurgeStandby.Should().BeTrue();

        var healthyCache = AdaptiveRamPolicy.Decide(
            Snap(88, freeMb: 500, standbyMb: 300), LongAgo, -1);
        healthyCache.PurgeStandby.Should().BeFalse();
    }

    [Fact]
    public void Game_running_halves_cooldown_and_earns_deep_at_high_pressure()
    {
        // 3 minutes since last clean: too soon on desktop (6m), fine in game (3m) — and
        // in-game high pressure goes straight to Deep (minimized apps are fair game).
        var desktop = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(3.5), -1);
        desktop.Clean.Should().BeFalse();

        var inGame = AdaptiveRamPolicy.Decide(
            Snap(88) with { GameRunning = true }, TimeSpan.FromMinutes(3.5), -1);
        inGame.Clean.Should().BeTrue();
        inGame.Mode.Should().Be(RamCleanMode.Deep);
    }

    [Fact]
    public void Fast_rising_trend_bumps_the_pressure_level()
    {
        // 78% is normally below the 'high' band; climbing 6pp/min treats it as high.
        var stable = AdaptiveRamPolicy.Decide(Snap(78), TimeSpan.FromMinutes(7), -1);
        var rising = AdaptiveRamPolicy.Decide(
            Snap(78) with { TrendPercentPerMin = 6 }, TimeSpan.FromMinutes(7), -1);

        stable.Clean.Should().BeFalse();   // elevated band wants a 10m cooldown
        rising.Clean.Should().BeTrue();    // bumped to high: 6m cooldown already met
        rising.Reason.Should().Contain("rising fast");
    }

    [Fact]
    public void Consecutive_ineffective_cleans_stretch_the_cooldown()
    {
        // High pressure, 8 minutes since last clean: normally due (6m) — but after two
        // no-yield cleans the wait stretches to 18m, so it holds off.
        var fresh = AdaptiveRamPolicy.Decide(Snap(88), TimeSpan.FromMinutes(8), -1);
        fresh.Clean.Should().BeTrue();

        var futile = AdaptiveRamPolicy.Decide(
            Snap(88), TimeSpan.FromMinutes(8), 10_000_000, consecutiveIneffectiveCleans: 2);
        futile.Clean.Should().BeFalse();
        futile.Reason.Should().Contain("cooling down");
    }

    [Fact]
    public void In_game_purge_gate_is_looser()
    {
        // 1 GB cache: below the 1.5 GB desktop trigger, above the 768 MB in-game one.
        // Free must sit between the 800 MB critical floor and the 1 GB purge floor,
        // or the critical band takes over and loosens the gate for everyone.
        var desktop = AdaptiveRamPolicy.Decide(
            Snap(88, freeMb: 900, standbyMb: 1024), LongAgo, -1);
        desktop.PurgeStandby.Should().BeFalse();

        var inGame = AdaptiveRamPolicy.Decide(
            Snap(88, freeMb: 900, standbyMb: 1024) with { GameRunning = true }, LongAgo, -1);
        inGame.PurgeStandby.Should().BeTrue();
    }

    [Fact]
    public void Purge_can_fire_during_clean_cooldown()
    {
        // Free memory starved + hoarding cache, but a clean ran 2 minutes ago: the purge
        // still goes (it's cheap and instant); only the trim waits.
        var d = AdaptiveRamPolicy.Decide(
            Snap(88, freeMb: 500, standbyMb: 2000), TimeSpan.FromMinutes(2), -1);
        d.Clean.Should().BeFalse();
        d.PurgeStandby.Should().BeTrue();
    }
}
