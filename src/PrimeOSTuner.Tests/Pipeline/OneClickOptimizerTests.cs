using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Pipeline;

public class OneClickOptimizerTests
{
    private static Mock<ITweak> StubTweak(string id, bool succeeds = true)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.SetupGet(t => t.IsDestructive).Returns(false);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(succeeds ? TweakResult.Success("undo") : TweakResult.Failure("nope"));
        return m;
    }

    [Fact]
    public async Task Run_applies_all_safe_tweaks_and_records_each_in_history()
    {
        var tweaks = new[] { StubTweak("a").Object, StubTweak("b").Object };
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var history = new TweakHistory(historyPath);

        var optimizer = new OneClickOptimizer(tweaks, history);
        var report = await optimizer.RunAsync();

        report.SuccessCount.Should().Be(2);
        (await history.LoadAsync()).Should().HaveCount(2);

        File.Delete(historyPath);
    }

    [Fact]
    public async Task Run_skips_destructive_tweaks()
    {
        var safe = StubTweak("safe").Object;
        var destructive = StubTweak("danger");
        destructive.SetupGet(t => t.IsDestructive).Returns(true);

        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var optimizer = new OneClickOptimizer(new[] { safe, destructive.Object }, new TweakHistory(historyPath));

        var report = await optimizer.RunAsync();

        report.AppliedTweakIds.Should().BeEquivalentTo(new[] { "safe" });
        report.SkippedDestructiveCount.Should().Be(1);

        File.Delete(historyPath);
    }
}
