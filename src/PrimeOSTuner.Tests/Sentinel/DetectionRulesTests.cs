using FluentAssertions;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class DetectionRulesTests
{
    private static readonly DateTime Now = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private static MetricsSnapshot Snap(
        double cpu = 10, long ramUsed = 4L * 1024 * 1024 * 1024, long ramTotal = 16L * 1024 * 1024 * 1024,
        long vramUsed = 2L * 1024 * 1024 * 1024, long vramTotal = 12L * 1024 * 1024 * 1024)
        => new(Now, GamePid: 1234, cpu, ramUsed, ramTotal, vramUsed, vramTotal);

    [Fact]
    public void Vram_overhead_fires_when_usage_is_high_and_game_only_needs_a_little()
    {
        // 11.9 GB of 12 GB used (≈ 96.8 %, above the 95 % watermark), game's recommended is 4 GB.
        var snap = Snap(vramUsed: 11_900L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 4096);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_does_not_fire_when_game_actually_needs_lots_of_vram()
    {
        // 11.5 GB of 12 GB used, but game's recommended is 10 GB — expected.
        var snap = Snap(vramUsed: 11_500L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 10_240);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_stays_silent_when_RecVramMb_is_unknown()
    {
        var snap = Snap(vramUsed: 11_500L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_stays_silent_when_sampler_reports_unknown_vram()
    {
        var snap = Snap(vramUsed: -1, vramTotal: -1);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 4096);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Ram_pressure_fires_when_usage_is_high_and_game_only_needs_a_little()
    {
        // 15.7 GB of 16 GB used (≈ 95.8 %, above the 95 % watermark), game's recommended is 8 GB.
        var snap = Snap(ramUsed: 15_700L * 1024 * 1024, ramTotal: 16L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, RecRamMb: 8192, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Ram_pressure_does_not_fire_when_game_legitimately_needs_lots_of_ram()
    {
        var snap = Snap(ramUsed: 15_500L * 1024 * 1024, ramTotal: 16L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, RecRamMb: 16_384, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Cpu_saturated_fires_when_all_samples_in_30s_window_exceed_90_percent()
    {
        // Nine consecutive 91% samples over the last 32 s — strictly more than the 30 s window.
        var window = new Queue<(DateTime, double)>();
        for (int i = 8; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 91.0));

        var snap = Snap(cpu: 91);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Cpu_saturated_does_not_fire_when_one_sample_dipped_below_90()
    {
        // Nine samples (32 s span) — same as the firing case but one mid-window dip kills the rule.
        var window = new Queue<(DateTime, double)>();
        for (int i = 8; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), i == 4 ? 50.0 : 91.0));

        var snap = Snap(cpu: 91);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Cpu_saturated_does_not_fire_when_window_is_too_short()
    {
        // Only 3 samples (8 s span) — not yet 30 s of data.
        var window = new Queue<(DateTime, double)>();
        for (int i = 2; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 99.0));

        var snap = Snap(cpu: 99);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Ram_pressure_stays_silent_when_RecRamMb_is_unknown()
    {
        var snap = Snap(ramUsed: 15_700L * 1024 * 1024, ramTotal: 16L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, RecRamMb: null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Ram_pressure_stays_silent_when_sampler_reports_unknown_ram()
    {
        var snap = Snap(ramUsed: -1, ramTotal: -1);
        var spec = new SteamPcRequirements(null, RecRamMb: 8192, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Cpu_saturated_stays_silent_when_sampler_reports_unknown_cpu()
    {
        var window = new Queue<(DateTime, double)>();
        for (int i = 8; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 91.0));

        var snap = Snap(cpu: -1);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Cpu_saturated_does_not_fire_when_current_snapshot_sits_exactly_on_the_watermark()
    {
        // 9 samples all at 91% (above), but the current snap is exactly 90.0 — strict-greater rule
        // means the *current* tick must exceed 90, so this should stay silent.
        var window = new Queue<(DateTime, double)>();
        for (int i = 8; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 91.0));

        var snap = Snap(cpu: 90.0);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }
}
