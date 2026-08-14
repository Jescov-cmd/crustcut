using FluentAssertions;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class OneClickOptimizerOptInTests
{
    private sealed class FakeTweak : ITweak, IOptInTweak
    {
        public FakeTweak(string id, bool optIn) { Id = id; OptIn = optIn; }
        public string Id { get; }
        public bool OptIn { get; }
        public bool Applied { get; private set; }
        public string DisplayName => Id;
        public string Description => "";
        public bool RequiresElevation => false;
        public bool IsDestructive => false;
        public bool RequiresReboot => false;
        public Task<TweakState> ProbeAsync(CancellationToken ct = default)
            => Task.FromResult(TweakState.NotApplied);
        public Task<TweakResult> ApplyAsync(IProgress<int>? p = null, CancellationToken ct = default)
        {
            Applied = true;
            return Task.FromResult(TweakResult.Success());
        }
        public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
            => Task.FromResult(TweakResult.Success());
        public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult("");
    }

    [Fact]
    public async Task One_click_never_applies_opt_in_tweaks()
    {
        // MPO disable helps some machines and paints black rectangles on others; a bundle
        // must not make that bet for the user.
        var ordinary = new FakeTweak("core.ordinary", optIn: false);
        var machineDependent = new FakeTweak("game.mpo-disable", optIn: true);
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid():N}.json");

        try
        {
            var optimizer = new OneClickOptimizer(
                new ITweak[] { ordinary, machineDependent }, new TweakHistory(historyPath));
            await optimizer.RunAsync();

            ordinary.Applied.Should().BeTrue();
            machineDependent.Applied.Should().BeFalse();
        }
        finally { try { File.Delete(historyPath); } catch { } }
    }

    /// <summary>
    /// Game Boost profiles are bundles too. The opt-in filter was added to OPTIMIZE NOW
    /// after the MPO incident, but profiles kept applying opt-in tweaks — the same wound
    /// through a different door (Performance mode shipped hardware GPU scheduling and the
    /// desktop restyle). A profile must skip them and still apply the rest.
    /// </summary>
    [Fact]
    public async Task Profiles_never_apply_opt_in_tweaks()
    {
        var ordinary = new FakeTweak("core.ordinary", optIn: false);
        var machineDependent = new FakeTweak("game.hw-gpu-scheduling", optIn: true);
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid():N}.json");

        try
        {
            var profile = new PrimeOSTuner.Core.Profiles.ModeProfile(
                "test", "Test", "", new[] { "core.ordinary", "game.hw-gpu-scheduling" });
            var applier = new PrimeOSTuner.Core.Profiles.ProfileApplier(
                new ITweak[] { ordinary, machineDependent }, new TweakHistory(historyPath));

            var result = await applier.ApplyAsync(profile);

            ordinary.Applied.Should().BeTrue();
            machineDependent.Applied.Should().BeFalse();
            result.FailureCount.Should().Be(0);   // skipped, not failed
        }
        finally { try { File.Delete(historyPath); } catch { } }
    }

    /// <summary>
    /// Built-in profile definitions must not even LIST opt-in tweak ids — the applier
    /// filter is the backstop, not the excuse.
    /// </summary>
    [Fact]
    public void Built_in_profiles_do_not_list_opt_in_tweaks()
    {
        string[] optInIds = { "game.hw-gpu-scheduling", "core.visual-effects",
                              "core.cpu-boost-limit", "game.mpo-disable" };
        foreach (var profile in PrimeOSTuner.Core.Profiles.BuiltInProfiles.All)
            profile.TweakIds.Should().NotIntersectWith(optInIds,
                because: $"profile '{profile.Id}' is a bundle and bundles never apply opt-in tweaks");
    }

    /// <summary>
    /// The specific tweaks that have already burned users must STAY opt-in. Each earned
    /// its place here by shipping in a bundle and visibly breaking machines: MPO disable
    /// and hardware GPU scheduling paint black boxes on some GPU combinations, the
    /// visual-effects tweak restyles the desktop, and Quiet CPU trades speed for silence.
    /// If someone removes IOptInTweak from one of these during a refactor, this test is
    /// what stops the regression from shipping.
    /// </summary>
    [Fact]
    public void Tweaks_that_burned_users_stay_opt_in()
    {
        var registry = new Moq.Mock<PrimeOSTuner.Win.IRegistryClient>().Object;
        var power = new Moq.Mock<PrimeOSTuner.Win.IPowerPlanClient>().Object;

        new HwGpuSchedulingTweak(registry).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
        new VisualEffectsTweak(registry).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
        new CpuBoostLimitTweak(power).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
    }
}
