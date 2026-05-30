using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// Proves the volatile timer-resolution tweak actually works on the real system, and
/// clears it afterward. This is the tweak that gets re-applied on startup via
/// SessionTweakStore, so it must apply + probe correctly for that mechanism to be useful.
/// </summary>
[Trait("Category", "Integration")]
public class TimerResolutionRealSystemTests
{
    [Fact]
    public async Task Apply_drives_probe_to_Applied_then_clears()
    {
        var tweak = new TimerResolutionTweak(new TimerResolutionClient());
        try
        {
            var result = await tweak.ApplyAsync();
            result.Succeeded.Should().BeTrue();

            // While the requesting process holds it, the system timer is at the target —
            // exactly the situation after startup re-applies it and the app stays resident.
            (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
        }
        finally
        {
            // Release our timer-resolution request so we don't leave the system timer raised.
            await tweak.RevertAsync("5000");
        }
    }
}
