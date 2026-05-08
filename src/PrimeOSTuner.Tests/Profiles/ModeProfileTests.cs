using System.Text.Json;
using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ModeProfileTests
{
    [Fact]
    public void ModeProfile_round_trips_through_json()
    {
        var profile = new ModeProfile(
            Id: "basic",
            DisplayName: "Basic Mode",
            Description: "Light gaming preset",
            TweakIds: new[] { "game.game-mode", "game.mouse-accel" });

        var json = JsonSerializer.Serialize(profile);
        var round = JsonSerializer.Deserialize<ModeProfile>(json);

        round.Should().NotBeNull();
        round!.Id.Should().Be("basic");
        round.TweakIds.Should().BeEquivalentTo(new[] { "game.game-mode", "game.mouse-accel" });
    }
}
