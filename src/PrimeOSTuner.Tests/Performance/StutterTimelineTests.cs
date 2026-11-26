using FluentAssertions;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class StutterTimelineTests
{
    private static readonly DateTime T0 = new(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>60 s of 60 fps; the listed seconds get one 120 ms hitch. Each second's
    /// system reading comes from <paramref name="system"/>.</summary>
    private static (StutterTimeline Timeline, List<double> Frames) Session(
        int[] stutterSeconds, Func<int, (double Cpu, double Gpu, double Ram, double PageIn)> system)
    {
        var t = new StutterTimeline(T0);
        var frames = new List<double>();
        for (var s = 0; s < 60; s++)
        {
            for (var f = 0; f < 60; f++)
            {
                var ms = stutterSeconds.Contains(s) && f == 30 ? 120 : 16.7;
                frames.Add(ms);
                t.AddFrame(T0.AddSeconds(s + f / 60.0), ms);
            }
            var (cpu, gpu, ram, page) = system(s);
            t.AddSystem(T0.AddSeconds(s + 1.01), cpu, gpu, ram, page);
        }
        return (t, frames);
    }

    [Fact]
    public void Stutters_while_paging_are_blamed_on_memory_and_name_the_hogs()
    {
        var hitches = new[] { 5, 12, 20, 33, 47 };
        var (t, frames) = Session(hitches, s => hitches.Contains(s) ? (40, 60, 94, 2500) : (40, 60, 80, 20));

        var r = t.Analyze(frames, new[] { new MemoryHog("Code", 3_500_000_000), new MemoryHog("node", 1_500_000_000) });

        r.StutterSeconds.Should().Be(5);
        r.MemorySeconds.Should().Be(5);
        r.PeakRamPercent.Should().Be(94);
        r.Verdict.Should().Contain("5 of 5").And.Contain("RAM").And.Contain("Code 3.3 GB");
    }

    /// <summary>
    /// Heavy page-ins with plenty of RAM free are files being read in (a loading screen,
    /// a program starting), not memory running out. Blaming memory there is exactly the
    /// misreading the user caught on 2026-09-25.
    /// </summary>
    [Fact]
    public void Heavy_disk_reads_with_ram_to_spare_are_not_blamed_on_memory()
    {
        var hitches = new[] { 5, 12, 20, 33 };
        var (t, frames) = Session(hitches, s => hitches.Contains(s) ? (40, 60, 62, 5000) : (40, 60, 62, 20));

        var r = t.Analyze(frames, Array.Empty<MemoryHog>());

        r.MemorySeconds.Should().Be(0);
        r.Verdict.Should().NotContain("RAM was full");
    }

    [Fact]
    public void Stutters_with_a_maxed_graphics_card_are_blamed_on_the_gpu()
    {
        var hitches = new[] { 3, 9, 15, 27 };
        var (t, frames) = Session(hitches, s => hitches.Contains(s) ? (50, 99, 70, 10) : (50, 80, 70, 10));

        var r = t.Analyze(frames, Array.Empty<MemoryHog>());

        r.GpuSeconds.Should().Be(4);
        r.Verdict.Should().Contain("graphics card");
    }

    [Fact]
    public void Stutters_on_an_idle_machine_point_at_the_game_not_the_pc()
    {
        var hitches = new[] { 4, 8, 16, 32 };
        var (t, frames) = Session(hitches, _ => (30, 50, 60, 5));

        t.Analyze(frames, Array.Empty<MemoryHog>()).Verdict.Should().Contain("no single cause");
    }

    /// <summary>
    /// The user's Roblox session, 2026-09-25: 320 stutter seconds that CPU%, GPU% and RAM
    /// could not explain, while a reset network card held core 0 at 12-22% DPC time.
    /// </summary>
    [Fact]
    public void Stutters_while_a_driver_hogs_a_core_point_at_the_network_card_fix()
    {
        var t = new StutterTimeline(T0);
        var frames = new List<double>();
        var hitches = new[] { 6, 14, 22, 38, 51 };
        for (var s = 0; s < 60; s++)
        {
            for (var f = 0; f < 60; f++)
            {
                var ms = hitches.Contains(s) && f == 30 ? 90 : 16.7;
                frames.Add(ms);
                t.AddFrame(T0.AddSeconds(s + f / 60.0), ms);
            }
            t.AddSystem(T0.AddSeconds(s + 1.01), 30, 60, 75, 10, maxCoreDpcPercent: hitches.Contains(s) ? 21 : 1);
        }

        var r = t.Analyze(frames, Array.Empty<MemoryHog>());

        r.DriverSeconds.Should().Be(5);
        r.PeakDriverPercent.Should().Be(21);
        r.Verdict.Should().Contain("driver").And.Contain("Network card latency fix");
    }

    [Fact]
    public void Paging_is_blamed_on_memory_even_though_it_also_raises_driver_time()
    {
        var t = new StutterTimeline(T0);
        var frames = new List<double>();
        for (var s = 0; s < 40; s++)
        {
            for (var f = 0; f < 60; f++)
            {
                var ms = s % 10 == 5 && f == 0 ? 100 : 16.7;
                frames.Add(ms);
                t.AddFrame(T0.AddSeconds(s + f / 60.0), ms);
            }
            t.AddSystem(T0.AddSeconds(s + 1.01), 30, 60, 94, 3000, maxCoreDpcPercent: 12);
        }

        var r = t.Analyze(frames, Array.Empty<MemoryHog>());
        r.MemorySeconds.Should().Be(4);
        r.DriverSeconds.Should().Be(0);
    }

    private const long Gb = 1L << 30;

    [Fact]
    public void A_game_allowed_too_much_of_a_small_pcs_ram_gets_a_concrete_number()
    {
        // 8 GB Minecraft on a 15 GB PC: the heap alone takes over half the machine.
        var advice = StutterTimeline.HeapAdvice(8 * Gb, (long)(15.2 * Gb));
        advice.Should().Contain("8 GB").And.Contain("try 5 GB");
    }

    [Theory]
    [InlineData(4L, 15.2)]    // sensible already
    [InlineData(6L, 32.0)]    // plenty of RAM for it
    public void A_reasonable_game_memory_setting_gets_no_advice(long heapGb, double totalGb)
        => StutterTimeline.HeapAdvice(heapGb * Gb, (long)(totalGb * Gb)).Should().BeEmpty();

    [Fact]
    public void Never_suggests_less_than_modded_minecraft_needs()
        => StutterTimeline.HeapAdvice(6 * Gb, 8 * Gb).Should().Contain("try 4 GB");

    [Fact]
    public void A_smooth_session_says_so()
    {
        var (t, frames) = Session(Array.Empty<int>(), _ => (30, 50, 60, 5));

        var r = t.Analyze(frames, Array.Empty<MemoryHog>());

        r.StutterSeconds.Should().Be(0);
        r.Verdict.Should().StartWith("Smooth");
    }

    [Fact]
    public void Short_sessions_are_not_judged()
    {
        StutterTimeline.Explain(seconds: 10, stutters: 5, memory: 5, gpu: 0, cpu: 0, 95, Array.Empty<MemoryHog>())
            .Should().Be("Too short to judge.");
    }

    [Fact]
    public void A_high_frame_rate_dip_is_not_a_stutter()
    {
        // 240 fps with one 16 ms frame: slower, but still faster than 60 fps.
        var t = new StutterTimeline(T0);
        var frames = new List<double>();
        for (var s = 0; s < 40; s++)
            for (var f = 0; f < 240; f++)
            {
                var ms = f == 0 ? 16 : 4.2;
                frames.Add(ms);
                t.AddFrame(T0.AddSeconds(s + f / 240.0), ms);
            }

        t.Analyze(frames, Array.Empty<MemoryHog>()).StutterSeconds.Should().Be(0);
    }

    /// <summary>
    /// Minecraft's javaw.exe is one of many Java processes on a modder's machine. Taking
    /// the first by name recorded Gradle daemons instead of the game.
    /// </summary>
    [Fact]
    public void The_game_process_is_the_one_with_a_window_then_the_biggest()
    {
        ProfileLifecycleService.PickGamePid(new[]
        {
            (Pid: 100, HasWindow: false, Memory: 2_000_000_000L),   // Gradle daemon
            (Pid: 200, HasWindow: true,  Memory: 1_500_000_000L),   // the game
            (Pid: 300, HasWindow: false, Memory: 300_000_000L),
        }).Should().Be(200);

        ProfileLifecycleService.PickGamePid(new[]
        {
            (Pid: 1, HasWindow: false, Memory: 10L),
            (Pid: 2, HasWindow: false, Memory: 90L),
        }).Should().Be(2);

        ProfileLifecycleService.PickGamePid(Array.Empty<(int, bool, long)>()).Should().Be(0);
    }
}
