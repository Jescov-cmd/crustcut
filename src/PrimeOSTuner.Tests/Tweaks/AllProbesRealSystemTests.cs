using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;
using Xunit.Abstractions;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// Runtime check against the REAL machine: build every toggle optimizer with real clients
/// and call ProbeAsync. Each must return a DEFINITE state (Applied or NotApplied) without
/// throwing. This is the invariant that catches the whole class of "applies fine but the
/// tile reads wrong" bugs — e.g. CPU core parking, whose probe used to return Unknown
/// because powercfg /query can't read the hidden setting. Read-only, no elevation needed.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RealSystemRegistry")]
public sealed class AllProbesRealSystemTests
{
    private readonly ITestOutputHelper _out;
    public AllProbesRealSystemTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> ToggleTweaks()
    {
        var reg = new RegistryClient();
        var power = new PowerPlanClient();
        var svc = new ServiceClient();

        var hardcoded = new ITweak[]
        {
            new GameModeTweak(reg),
            new HwGpuSchedulingTweak(reg),
            new MouseAccelTweak(reg),
            new VisualEffectsTweak(reg),
            new SnappyUiTweak(reg),
            new StickyKeysTweak(reg),
            new MmcssGamesPriorityTweak(reg),
            new WidgetsDisableTweak(reg),
            new CortanaDisableTweak(reg),
            new HibernationTweak(reg, power),
            new CpuCoreParkingTweak(power),
            new PowerPlanTweak(power),
            new UltimatePerformanceTweak(power),
            new TelemetryDisableTweak(reg, svc),
        };

        var catalog = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath())
            .Select(d => (ITweak)new RegistryTweak(d, reg));

        return hardcoded.Concat(catalog).Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(ToggleTweaks))]
    public async Task Probe_returns_a_definite_state_on_the_real_system(ITweak tweak)
    {
        TweakState state;
        try { state = await tweak.ProbeAsync(); }
        catch (Exception ex) { throw new Xunit.Sdk.XunitException($"{tweak.Id} probe threw: {ex.Message}"); }

        _out.WriteLine($"{tweak.Id,-34} -> {state}");

        // Toggle optimizers must report Applied or NotApplied. "Unknown" means the probe
        // can't read its own setting back — exactly the CPU-core-parking failure mode.
        state.Should().BeOneOf(new[] { TweakState.Applied, TweakState.NotApplied },
            $"{tweak.Id} must report a definite on/off state so its tile is accurate");
    }
}
