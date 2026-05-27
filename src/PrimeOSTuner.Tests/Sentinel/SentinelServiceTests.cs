using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class SentinelServiceTests
{
    private static KnownGame Game(string id = "g1", string? appId = "1091500") =>
        new(id, "Test Game", new[] { "test.exe" }, appId, "C:\\Games\\Test", KnownGameSource.Steam);

    private static MetricsSnapshot Snap(DateTime at, double cpu = 10,
        long vramUsed = 2L * 1024 * 1024 * 1024, long vramTotal = 12L * 1024 * 1024 * 1024,
        long ramUsed = 4L * 1024 * 1024 * 1024, long ramTotal = 16L * 1024 * 1024 * 1024)
        => new(at, 1234, cpu, ramUsed, ramTotal, vramUsed, vramTotal);

    [Fact]
    public async Task OnGameStarted_fetches_spec_and_starts_publishing_problems()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync("1091500", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, RecVramMb: 4096));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, vramUsed: 11_900L * 1024 * 1024));

        var service = new SentinelService(fetcher.Object, sampler.Object);
        var changed = false;
        service.Changed += (_, _) => changed = true;

        service.OnGameStarted(Game(), pid: 1234);
        // Give the fire-and-forget spec fetch a tick to land before the first sample.
        await Task.Delay(20);
        await service.TickOnceAsync();

        service.WatchingGame.Should().Be("Test Game");
        service.Currently.Should().ContainSingle(p => p.Kind == ProblemKind.VramOverhead);
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task OnGameStopped_clears_state_and_raises_Changed()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, RecVramMb: 4096));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, vramUsed: 11_900L * 1024 * 1024));

        var service = new SentinelService(fetcher.Object, sampler.Object);
        service.OnGameStarted(Game(), pid: 1234);
        await Task.Delay(20);
        await service.TickOnceAsync();

        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;
        service.OnGameStopped();

        service.WatchingGame.Should().BeNull();
        service.Currently.Should().BeEmpty();
        changedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OnGameStarted_with_no_app_id_skips_spec_fetch_but_still_runs_cpu_rule()
    {
        var fetcher = new Mock<ISpecFetcher>(MockBehavior.Strict); // any call would throw
        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

        var sampler = new Mock<IMetricsSampler>();
        var ticks = 0;
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time.AddSeconds(ticks++ * 4), cpu: 95));

        var service = new SentinelService(fetcher.Object, sampler.Object);

        service.OnGameStarted(Game(appId: null), pid: 1234);

        // 9 ticks at 4 s each spans 32 s of sustained 95% CPU → CpuSaturated fires.
        for (int i = 0; i < 9; i++) await service.TickOnceAsync();

        service.Currently.Should().ContainSingle(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public async Task Changed_fires_every_tick_because_LatestSnapshot_is_always_fresh()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, null));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, cpu: 10));

        var service = new SentinelService(fetcher.Object, sampler.Object);
        service.OnGameStarted(Game(), pid: 1234);
        await service.TickOnceAsync();

        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;

        await service.TickOnceAsync();
        await service.TickOnceAsync();

        changedCount.Should().Be(2);   // every tick raises Changed so the VM can refresh live values
    }

    [Fact]
    public void Disabling_clears_state_and_subsequent_OnGameStarted_is_a_no_op()
    {
        var fetcher = new Mock<ISpecFetcher>();
        var sampler = new Mock<IMetricsSampler>();
        var service = new SentinelService(fetcher.Object, sampler.Object);
        service.OnGameStarted(Game(), pid: 1234);
        service.WatchingGame.Should().NotBeNull();

        service.Enabled = false;
        service.WatchingGame.Should().BeNull();

        service.OnGameStarted(Game(), pid: 1234);
        service.WatchingGame.Should().BeNull();
    }

    [Fact]
    public void Re_enabling_allows_OnGameStarted_to_take_effect_again()
    {
        var fetcher = new Mock<ISpecFetcher>();
        var sampler = new Mock<IMetricsSampler>();
        var service = new SentinelService(fetcher.Object, sampler.Object);

        service.OnGameStarted(Game(), pid: 1234);
        service.WatchingGame.Should().NotBeNull();
        service.Enabled = false;
        service.WatchingGame.Should().BeNull();

        service.Enabled = true;
        service.OnGameStarted(Game(id: "g2"), pid: 5678);
        service.WatchingGame.Should().Be("Test Game");
    }
}
