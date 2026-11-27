using FluentAssertions;
using PrimeOSTuner.Core.Fans;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Fans;

/// <summary>
/// The governor's boost trim against a simulated set of power schemes — the per-scheme
/// trap: settings belong to a scheme, and Game mode switches to (and freshly creates)
/// the Ultimate Performance scheme while the CPU may be trimmed.
/// </summary>
public class BoostTrimTests
{
    private static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid HighPerf = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid Ultimate = new("11111111-2222-3333-4444-555555555555");

    /// <summary>Power schemes in memory: per-scheme boost value, active scheme, powercfg writes.</summary>
    private sealed class FakePower : IPowerPlanClient
    {
        public readonly Dictionary<Guid, (string Name, int? Boost)> Schemes = new();
        public Guid Active;

        public int? Boost(Guid g) => Schemes[g].Boost;

        public IReadOnlyList<PowerPlan> ListPlans() =>
            Schemes.Select(kv => new PowerPlan(kv.Key, kv.Value.Name)).ToList();
        public PowerPlan GetActivePlan() => new(Active, Schemes[Active].Name);
        public void SetActivePlan(Guid planGuid) => Active = planGuid;
        public Guid EnsureUltimatePerformancePlan() => throw new NotSupportedException();
        public void SetActiveAcValueIndex(string subgroup, string setting, int value) => throw new NotSupportedException();
        public int? GetActiveAcValueIndex(string subgroup, string setting) => throw new NotSupportedException();
        public int? GetActiveSchemeSettingIndexFromRegistry(string subgroupGuid, string settingGuid) => Boost(Active);
        public int? GetSchemeSettingIndexFromRegistry(Guid scheme, string subgroupGuid, string settingGuid)
            => Schemes.TryGetValue(scheme, out var s) ? s.Boost : null;

        public IReadOnlyDictionary<Guid, int?> SetValueIndexOnAllSchemes(string subgroup, string setting, int value)
        {
            var prev = Schemes.ToDictionary(kv => kv.Key, kv => kv.Value.Boost);
            foreach (var g in Schemes.Keys.ToList()) Set(g, value);
            return prev;
        }

        public void RestoreValueIndexPerScheme(string subgroup, string setting,
            IReadOnlyDictionary<Guid, int?> previous, int fallback)
        {
            foreach (var g in Schemes.Keys.ToList())
                Set(g, previous.TryGetValue(g, out var old) ? old ?? fallback : fallback);
        }

        public string RunPowercfg(string args)
        {
            var parts = args.Split(' ');
            if (parts[0] is "/setacvalueindex" or "/setdcvalueindex")
            {
                parts[2].Should().Be(ThermalGovernor.SubProcessorGuid);
                parts[3].Should().Be(ThermalGovernor.PerfBoostModeGuid);
                var g = Guid.Parse(parts[1]);
                if (!Schemes.ContainsKey(g)) throw new InvalidOperationException("no such scheme");
                Set(g, int.Parse(parts[4]));
            }
            return "";
        }

        private void Set(Guid g, int value) => Schemes[g] = (Schemes[g].Name, value);
    }

    private static FakePower ThreePlans()
    {
        var p = new FakePower();
        p.Schemes[Balanced] = ("Balanced", 2);
        p.Schemes[HighPerf] = ("High performance", 1);
        p.Schemes[Ultimate] = ("Ultimate Performance", null);   // never set → Windows default
        p.Active = Balanced;
        return p;
    }

    [Fact]
    public void Engage_trims_every_scheme_and_restore_gives_each_its_own_value_back()
    {
        var power = ThreePlans();
        var trim = new BoostTrim(power, _ => { });

        trim.Engage().Should().BeTrue();
        power.Schemes.Values.Should().OnlyContain(s => s.Boost == ThermalGovernor.BoostDisabled);

        trim.Restore();
        power.Boost(Balanced).Should().Be(2);
        power.Boost(HighPerf).Should().Be(1, "each scheme gets ITS value, not the active one's");
        power.Boost(Ultimate).Should().Be(ThermalGovernor.BoostWindowsDefault);
        trim.Active.Should().BeFalse();
    }

    [Fact]
    public void A_plan_created_and_activated_while_trimmed_gets_trimmed_too()
    {
        // The Game mode hole: trimmed on Balanced, then the game launches, Ultimate
        // Performance is duplicated fresh from its template (boost on) and made active.
        var power = new FakePower();
        power.Schemes[Balanced] = ("Balanced", 2);
        power.Active = Balanced;
        var trim = new BoostTrim(power, _ => { });
        trim.Engage();

        power.Schemes[Ultimate] = ("Ultimate Performance", 2);
        power.Active = Ultimate;

        trim.FollowActivePlan().Should().BeTrue();
        power.Boost(Ultimate).Should().Be(ThermalGovernor.BoostDisabled);
        trim.FollowActivePlan().Should().BeFalse("already trimmed — nothing to do on the next tick");

        trim.Restore();
        power.Boost(Ultimate).Should().Be(2);
        power.Boost(Balanced).Should().Be(2);
    }

    [Fact]
    public void Switching_to_an_existing_plan_keeps_it_trimmed()
    {
        var power = ThreePlans();
        var trim = new BoostTrim(power, _ => { });
        trim.Engage();

        power.Active = Ultimate;   // Game mode's core.power-plan
        trim.FollowActivePlan().Should().BeFalse();
        power.Boost(Ultimate).Should().Be(ThermalGovernor.BoostDisabled);
    }

    [Fact]
    public void A_value_rewritten_while_trimmed_is_retrimmed_and_the_first_original_kept()
    {
        var power = ThreePlans();
        var trim = new BoostTrim(power, _ => { });
        trim.Engage();

        // Something (a tweak revert, another tool) writes boost back on the active plan.
        power.Schemes[Balanced] = ("Balanced", 3);
        trim.FollowActivePlan().Should().BeTrue();
        power.Boost(Balanced).Should().Be(ThermalGovernor.BoostDisabled);

        trim.Restore();
        power.Boost(Balanced).Should().Be(2, "the user's value from before the trim, not the intruder's");
    }

    [Fact]
    public void Restore_skips_deleted_schemes_and_schemes_changed_since()
    {
        var power = ThreePlans();
        var trim = new BoostTrim(power, _ => { });
        trim.Engage();

        power.Schemes.Remove(Ultimate);                         // Game mode exit deletes it
        power.Schemes[HighPerf] = ("High performance", 4);      // user changed it meanwhile

        trim.Restore();
        power.Boost(Balanced).Should().Be(2);
        power.Boost(HighPerf).Should().Be(4, "a later change by someone else wins");
        power.Schemes.Should().NotContainKey(Ultimate);
    }

    [Fact]
    public void Schemes_already_off_are_not_recorded_and_all_off_means_nothing_to_trim()
    {
        var power = ThreePlans();
        power.Schemes[HighPerf] = ("High performance", 0);
        var trim = new BoostTrim(power, _ => { });
        trim.Engage().Should().BeTrue();
        trim.Original.Should().NotContainKey(HighPerf);
        trim.Restore();
        power.Boost(HighPerf).Should().Be(0, "Quiet CPU's off is not ours to give back");

        var allOff = new FakePower();
        allOff.Schemes[Balanced] = ("Balanced", 0);
        allOff.Active = Balanced;
        new BoostTrim(allOff, _ => { }).Engage().Should().BeFalse();
    }

    [Fact]
    public void The_ledger_is_persisted_before_writing_and_survives_a_crash()
    {
        var power = ThreePlans();
        string? marker = null;
        var bootValueWhenPersisted = -1;
        var trim = new BoostTrim(power, s =>
        {
            marker = s;
            if (s is not null) bootValueWhenPersisted = power.Boost(Balanced) ?? -1;
        });
        trim.Engage();

        bootValueWhenPersisted.Should().Be(2, "the marker is written before any scheme changes");
        marker.Should().NotBeNull();

        // "Crash": a new instance on the next start recovers from the marker text alone.
        string? after = "untouched";
        new BoostTrim(power, s => after = s).RecoverFromMarker(marker!);
        power.Boost(Balanced).Should().Be(2);
        power.Boost(HighPerf).Should().Be(1);
        power.Boost(Ultimate).Should().Be(ThermalGovernor.BoostWindowsDefault);
        after.Should().BeNull("the marker is deleted once restored");
    }

    [Fact]
    public void A_marker_from_an_older_build_restores_its_single_value_everywhere()
    {
        var power = ThreePlans();
        power.SetValueIndexOnAllSchemes(ThermalGovernor.SubProcessorGuid, ThermalGovernor.PerfBoostModeGuid, 0);

        new BoostTrim(power, _ => { }).RecoverFromMarker("2\r\n");

        power.Schemes.Values.Should().OnlyContain(s => s.Boost == 2);
    }

    [Theory]
    [InlineData(true, false, false, true)]    // Cool-down assist on: always
    [InlineData(false, true, true, true)]     // Game mode on and a game running
    [InlineData(false, true, false, false)]   // Game mode on, no game
    [InlineData(false, false, true, false)]   // a game, but Game mode off
    public void Governor_runs_for_cool_down_assist_or_while_game_mode_has_a_game(
        bool assist, bool gameModeAuto, bool gameRunning, bool expected)
        => ThermalGovernor.ShouldRun(assist, gameModeAuto, gameRunning).Should().Be(expected);
}
