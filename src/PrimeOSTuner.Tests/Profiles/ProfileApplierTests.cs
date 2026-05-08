using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ProfileApplierTests
{
    private static Mock<ITweak> StubTweak(string id, bool succeeds = true)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(succeeds ? TweakResult.Success("undo-" + id) : TweakResult.Failure("nope"));
        m.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .ReturnsAsync(TweakResult.Success());
        return m;
    }

    [Fact]
    public async Task ApplyAsync_runs_each_resolved_tweak_in_order_and_records_history()
    {
        var a = StubTweak("a");
        var b = StubTweak("b");
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var history = new TweakHistory(historyPath);

        var applier = new ProfileApplier(new[] { a.Object, b.Object }, history);
        var profile = new ModeProfile("p", "P", "P", new[] { "a", "b" });

        var result = await applier.ApplyAsync(profile);

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
        result.Outcomes.Select(o => o.TweakId).Should().BeEquivalentTo(new[] { "a", "b" });
        (await history.LoadAsync()).Should().HaveCount(2);

        File.Delete(historyPath);
    }

    [Fact]
    public async Task ApplyAsync_skips_unknown_tweak_ids_and_records_failure()
    {
        var a = StubTweak("a");
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var applier = new ProfileApplier(new[] { a.Object }, new TweakHistory(historyPath));
        var profile = new ModeProfile("p", "P", "P", new[] { "a", "missing" });

        var result = await applier.ApplyAsync(profile);

        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);
        result.Outcomes.First(o => o.TweakId == "missing").Error.Should().Contain("not registered");

        File.Delete(historyPath);
    }

    [Fact]
    public async Task RevertAsync_calls_revert_on_each_outcome_in_reverse_order()
    {
        var a = StubTweak("a");
        var b = StubTweak("b");
        var calls = new List<string>();
        a.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .Callback<string, CancellationToken>((_, _) => calls.Add("a"))
            .ReturnsAsync(TweakResult.Success());
        b.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .Callback<string, CancellationToken>((_, _) => calls.Add("b"))
            .ReturnsAsync(TweakResult.Success());

        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var applier = new ProfileApplier(new[] { a.Object, b.Object }, new TweakHistory(historyPath));

        var outcomes = new[]
        {
            new ProfileTweakOutcome("a", true, "undo-a", null),
            new ProfileTweakOutcome("b", true, "undo-b", null),
        };
        await applier.RevertAsync(outcomes);

        calls.Should().Equal(new[] { "b", "a" });

        File.Delete(historyPath);
    }
}
