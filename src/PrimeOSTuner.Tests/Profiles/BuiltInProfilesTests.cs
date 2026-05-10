using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class BuiltInProfilesTests
{
    [Fact]
    public void Basic_includes_game_mode_mouse_accel_and_power_plan()
    {
        BuiltInProfiles.Basic.Id.Should().Be("basic");
        BuiltInProfiles.Basic.TweakIds.Should().Contain("game.game-mode");
        BuiltInProfiles.Basic.TweakIds.Should().Contain("game.mouse-accel");
        BuiltInProfiles.Basic.TweakIds.Should().Contain("core.power-plan");
    }

    [Fact]
    public void Performance_is_a_strict_superset_of_basic()
    {
        foreach (var t in BuiltInProfiles.Basic.TweakIds)
            BuiltInProfiles.Performance.TweakIds.Should().Contain(t);

        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.timer-resolution");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.hw-gpu-scheduling");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.nagle-algorithm");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.network-throttling");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.system-responsiveness");
    }

    [Fact]
    public void Aggressive_is_a_strict_superset_of_performance()
    {
        foreach (var t in BuiltInProfiles.Performance.TweakIds)
            BuiltInProfiles.Aggressive.TweakIds.Should().Contain(t);

        BuiltInProfiles.Aggressive.TweakIds.Should().Contain("core.ipv6-disable");
        BuiltInProfiles.Aggressive.TweakIds.Should().Contain("core.cortana-disable");
        BuiltInProfiles.Aggressive.TweakIds.Should().Contain("core.search-indexing-tune");
        BuiltInProfiles.Aggressive.TweakIds.Should().Contain("core.modern-standby-disable");
        BuiltInProfiles.Aggressive.TweakIds.Should().Contain("core.hibernation-disable");
    }

    [Fact]
    public void All_returns_basic_performance_and_aggressive_in_order()
    {
        BuiltInProfiles.All.Should().HaveCount(3);
        BuiltInProfiles.All[0].Id.Should().Be("basic");
        BuiltInProfiles.All[1].Id.Should().Be("performance");
        BuiltInProfiles.All[2].Id.Should().Be("aggressive");
    }

    [Fact]
    public void All_profile_tweak_ids_resolve_against_the_registered_tweak_set()
    {
        // Build the set of known ids that v0.4 ships.
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Existing custom tweaks
            "core.power-plan", "core.ram-cleaner", "core.dns-flush",
            "core.windows-update-cache", "core.driver-health",
            "core.driver-store-cleanup", "core.registry-cleanup-safe",
            "game.mouse-accel", "game.timer-resolution", "game.game-mode",
            "game.hw-gpu-scheduling", "game.nagle-algorithm",
            "game.network-throttling", "game.system-responsiveness",
            "game.cpu-core-parking", "game.per-app-gpu-pref",
            // New custom
            "core.telemetry-disable", "core.cortana-disable",
            "core.ultimate-performance", "core.hibernation-disable",
            "core.nic-power-mgmt",
            // Service disables
            "core.sysmain-disable", "core.search-indexing-tune",
            "core.connected-user-experiences",
            // Registry catalog (must match tweaks.json)
            "core.win32-priority-separation", "core.startup-delay",
            "core.tcp-ack-frequency", "core.tcp-delivery-acceleration",
            "core.qos-bandwidth",
            "core.netbios-disable", "core.ipv6-disable",
            "core.werror-reporting", "core.game-dvr-disable",
            "core.fullscreen-optimizations",
            "core.ceip-disable", "core.activity-history",
            "core.advertising-id", "core.location-tracking",
            "core.feedback-diagnostics", "core.typing-personalization",
            "core.usb-selective-suspend", "core.pcie-aspm-disable",
            "core.power-throttling-disable", "core.modern-standby-disable",
        };

        foreach (var profile in new[] { BuiltInProfiles.Basic, BuiltInProfiles.Performance, BuiltInProfiles.Aggressive })
        {
            foreach (var id in profile.TweakIds)
            {
                knownIds.Should().Contain(id, because: $"profile '{profile.Id}' references unknown id '{id}'");
            }
        }
    }
}
