using System;
using FluentAssertions;
using PrimeOSTuner.Core.Diagnosis;
using Xunit;

namespace PrimeOSTuner.Tests.Diagnosis;

public class DiagnosisRulesTests
{
    [Fact]
    public void Cpu_throttled_when_perf_below_80_under_load()
        => DiagnosisRules.EvaluateCpuThrottle(avgPerfPercent: 62)!.Severity
            .Should().Be(FindingSeverity.Problem);

    [Fact]
    public void Cpu_not_throttled_at_full_performance()
        => DiagnosisRules.EvaluateCpuThrottle(avgPerfPercent: 98)!.Severity
            .Should().Be(FindingSeverity.Passed);

    [Fact]
    public void Cpu_unknown_sample_returns_null()
        => DiagnosisRules.EvaluateCpuThrottle(avgPerfPercent: null).Should().BeNull();

    [Fact]
    public void Background_hog_flagged_over_15_percent_cpu()
    {
        var f = DiagnosisRules.EvaluateBackgroundHogs(new[]
        {
            new ProcSample("chrome", 22.0, 900L * 1024 * 1024),
            new ProcSample("steam", 1.0, 200L * 1024 * 1024),
        });
        f.Severity.Should().Be(FindingSeverity.Warning);
        f.Detail.Should().Contain("chrome");
    }

    [Fact]
    public void No_hogs_passes()
        => DiagnosisRules.EvaluateBackgroundHogs(new[] { new ProcSample("steam", 1.0, 1) })
            .Severity.Should().Be(FindingSeverity.Passed);

    [Fact]
    public void Ram_pressure_warns_over_85_percent()
        => DiagnosisRules.EvaluateRam(usedPercent: 91).Severity.Should().Be(FindingSeverity.Warning);

    [Fact]
    public void Disk_warns_under_15_percent_free()
        => DiagnosisRules.EvaluateDisk(freeBytes: 50, totalBytes: 1000).Severity
            .Should().Be(FindingSeverity.Warning);

    [Fact]
    public void Power_saver_plan_is_a_problem_with_optimize_link()
    {
        var f = DiagnosisRules.EvaluatePowerPlan("Power saver");
        f.Severity.Should().Be(FindingSeverity.Problem);
        f.NavTarget.Should().Be("Optimize");
    }

    [Fact]
    public void Gpu_driver_older_than_a_year_warns()
        => DiagnosisRules.EvaluateGpuDriver(DateTime.UtcNow.AddMonths(-18), DateTime.UtcNow)
            .Severity.Should().Be(FindingSeverity.Warning);
}
