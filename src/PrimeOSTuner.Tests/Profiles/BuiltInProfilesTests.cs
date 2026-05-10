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
        BuiltInProfiles.Basic.TweakIds.Should().BeEquivalentTo(new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan"
        });
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
    public void All_returns_basic_and_performance_in_order()
    {
        BuiltInProfiles.All.Should().HaveCount(2);
        BuiltInProfiles.All[0].Id.Should().Be("basic");
        BuiltInProfiles.All[1].Id.Should().Be("performance");
    }
}
