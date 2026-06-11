using System;
using System.Linq;
using FluentAssertions;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class LiveDetectionRulesTests
{
    private static (DateTime, double, double)[] Window(double cpu, double perf, int seconds = 25)
        => Enumerable.Range(0, seconds)
            .Select(i => (DateTime.UtcNow.AddSeconds(-seconds + i), cpu, perf)).ToArray();

    [Fact]
    public void Throttled_when_busy_and_perf_low_for_20s()
        => DetectionRules.TryCpuThrottled(Window(cpu: 75, perf: 60), DateTime.UtcNow)
            .Should().NotBeNull();

    [Fact]
    public void Not_throttled_when_idle_even_if_perf_low()
        => DetectionRules.TryCpuThrottled(Window(cpu: 10, perf: 60), DateTime.UtcNow)
            .Should().BeNull();

    [Fact]
    public void Not_throttled_at_full_performance()
        => DetectionRules.TryCpuThrottled(Window(cpu: 90, perf: 99), DateTime.UtcNow)
            .Should().BeNull();

    [Fact]
    public void Short_window_is_silent()
        => DetectionRules.TryCpuThrottled(Window(cpu: 90, perf: 50, seconds: 5), DateTime.UtcNow)
            .Should().BeNull();
}
