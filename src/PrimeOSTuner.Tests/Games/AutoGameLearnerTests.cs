using FluentAssertions;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class AutoGameLearnerTests
{
    private static readonly DateTime T0 = new(2026, 9, 26, 19, 0, 0, DateTimeKind.Utc);

    private sealed class Gpu : IGpuUsageByProcess
    {
        public Dictionary<int, double> Map { get; } = new();
        public IReadOnlyDictionary<int, double> GpuPercentByPid => Map;
    }

    private sealed class Harness
    {
        public ForegroundWindow? Front;
        public readonly Gpu Gpu = new();
        public readonly List<KnownGame> Learned = new();
        public HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase);
        public ProcessIdentity Identity = new("Hollowbrook.exe", @"D:\Games\Hollowbrook\Hollowbrook.exe", "Hollowbrook");
        public AutoGameLearner Learner { get; }

        public Harness() => Learner = new AutoGameLearner(
            () => Front, Gpu, _ => Identity, () => Known,
            g => { lock (Learned) Learned.Add(g); return Task.CompletedTask; },
            windowsDir: @"C:\Windows");

        /// <summary>Feeds one sample a second for the given span, then lets the
        /// background decision finish.</summary>
        public async Task Play(int seconds)
        {
            for (var s = 0; s <= seconds; s++) Learner.OnSample(T0.AddSeconds(s));
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task A_fullscreen_window_keeping_the_gpu_busy_is_learned_as_a_game()
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 1.0) };
        h.Gpu.Map[4242] = 60;

        await h.Play(12);

        h.Learned.Should().ContainSingle();
        h.Learned[0].DisplayName.Should().Be("Hollowbrook");
        h.Learned[0].ExecutableNames.Should().Equal("Hollowbrook.exe");
    }

    [Fact]
    public async Task A_brief_burst_is_not_enough()
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 1.0) };
        h.Gpu.Map[4242] = 60;

        await h.Play(5);

        h.Learned.Should().BeEmpty();
    }

    [Fact]
    public async Task A_windowed_game_needs_more_load_for_longer()
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 0.7) };
        h.Gpu.Map[4242] = 20;                   // below the windowed bar
        await h.Play(30);
        h.Learned.Should().BeEmpty();

        var h2 = new Harness { Front = new ForegroundWindow(4242, 0.7) };
        h2.Gpu.Map[4242] = 45;
        await h2.Play(22);
        h2.Learned.Should().ContainSingle();
    }

    [Theory]
    [InlineData("chrome.exe", @"C:\Program Files\Google\Chrome\chrome.exe")]    // fullscreen video
    [InlineData("javaw.exe", @"C:\java\bin\javaw.exe")]                          // Minecraft has its own detector
    [InlineData("LosslessScaling.exe", @"D:\Steam\LosslessScaling.exe")]        // draws over games
    [InlineData("wallpaper64.exe", @"D:\Steam\wallpaper_engine\wallpaper64.exe")]
    [InlineData("SomeTool.exe", @"C:\Windows\System32\SomeTool.exe")]            // part of Windows
    public async Task Things_that_fill_the_screen_but_are_not_games_are_never_learned(string exe, string path)
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 1.0), Identity = new(exe, path, null) };
        h.Gpu.Map[4242] = 90;

        await h.Play(15);

        h.Learned.Should().BeEmpty();
    }

    [Fact]
    public async Task An_already_known_game_is_not_added_twice()
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 1.0) };
        h.Known.Add("Hollowbrook.exe");
        h.Gpu.Map[4242] = 60;

        await h.Play(12);

        h.Learned.Should().BeEmpty();
    }

    [Fact]
    public async Task Low_gpu_use_on_a_fullscreen_window_is_not_a_game()
    {
        var h = new Harness { Front = new ForegroundWindow(4242, 1.0) };
        h.Gpu.Map[4242] = 3;                    // a fullscreen document or slideshow

        await h.Play(30);

        h.Learned.Should().BeEmpty();
    }
}
