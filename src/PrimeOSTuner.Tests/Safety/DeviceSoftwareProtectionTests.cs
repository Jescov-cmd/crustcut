using Crustcut.Presentation;
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Safety;
using PrimeOSTuner.Core.Suspension;
using PrimeOSTuner.Win.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Safety;

/// <summary>
/// The user's rule (2026-11-26): software that controls devices is never frozen,
/// suspended, slept, killed, deprioritised, memory-limited or trimmed. One test per path
/// that could do any of those. The names are the real process names found on his PC
/// (lghub_agent, SignalRgb, StreamDeck, Elgato.WaveLink...) plus GameSir Nexus's.
/// </summary>
public class DeviceSoftwareProtectionTests
{
    public static readonly TheoryData<string> DeviceApps = new()
    {
        "lghub_agent", "lghub", "lghub_updater", "lghub_system_tray", "logioptionsplus_agent",
        "SignalRgb", "SignalRgbLauncher", "StreamDeck", "GameSirNexus", "GameSir Nexus",
        "Elgato.WaveLink", "WavelinkSEService", "ElgatoAudioControlServer",
        "RtkAudUService64", "RtkUWP", "Voicemod", "windhawk", "TranslucentTB",
        "RazerAppEngine", "iCUE", "SteelSeriesGG", "OpenRGB", "NZXT CAM", "ArmouryCrate",
        "MSI Center", "WootilityV5", "NahimicService", "voicemeeterpro", "AutoHotkey64",
        "PowerToys.FancyZones", "nvcontainer", "NVDisplay.Container", "NVIDIA app",
        "RadeonSoftware", "DolbyDAX3API",
    };

    private static readonly string[] Four = { "lghub_agent", "SignalRgb", "StreamDeck", "GameSirNexus" };

    // ── The list itself ──────────────────────────────────────────────────────────────
    [Theory]
    [MemberData(nameof(DeviceApps))]
    public void Every_named_device_app_is_protected(string name) =>
        DeviceSoftware.IsProtected(name).Should().BeTrue();

    [Theory]
    [InlineData("LGHUB_AGENT")]
    [InlineData("lghub_agent.exe")]
    [InlineData(@"C:\Program Files\LGHUB\lghub_agent.exe")]
    [InlineData("signalrgb.EXE")]
    [InlineData("streamdeck")]
    public void Matching_ignores_case_extension_and_folder(string name) =>
        DeviceSoftware.IsProtected(name).Should().BeTrue();

    [Theory]
    [InlineData("OneDrive")]
    [InlineData("chrome")]
    [InlineData("Medal")]
    [InlineData("")]
    [InlineData(null)]
    public void Ordinary_apps_are_not_device_software(string? name) =>
        DeviceSoftware.IsProtected(name).Should().BeFalse();

    // ── Freezing: the sleep list (GameOnlyAppsService runs on GameOnlyPolicy) ──────────
    [Theory]
    [MemberData(nameof(DeviceApps))]
    public void Sleep_list_never_freezes_device_software(string name)
    {
        GameOnlyPolicy.CanFreeze(name).Should().BeFalse();
        GameOnlyPolicy.CanFreeze(name + ".exe").Should().BeFalse();
        GameOnlyPolicy.WhyAwake(name, DateTime.UtcNow, enabled: true, gameRunning: false,
                DateTime.MinValue, DateTime.MinValue, hasWindowOnScreen: false, DateTime.MinValue)
            .Should().Be("never put to sleep");
    }

    [Fact]
    public void Sleep_list_drops_device_apps_the_user_added_by_hand()
    {
        var effective = GameOnlyPolicy.Effective(
            userAdded: Four.Append("Slicer"), excluded: Array.Empty<string>(), runningNames: Four);

        effective.Should().Equal("Slicer");
    }

    // ── Freezing: pause-while-gaming and automatic Game mode ─────────────────────────
    [Fact]
    public void Pause_while_gaming_never_suspends_device_software_even_if_a_profile_names_it()
    {
        var suspended = new List<int>();
        var suspender = new Mock<IProcessSuspender>();
        suspender.Setup(s => s.Suspend(It.IsAny<int>())).Callback<int>(suspended.Add);
        var pids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["lghub_agent"] = 1, ["SignalRgb"] = 2, ["StreamDeck"] = 3, ["GameSirNexus"] = 4,
            ["OneDrive"] = 5,
        };
        var svc = new BackgroundSuspenderService(suspender.Object,
            targetNames: pids.Keys.ToList(),
            processIdsByName: n => new[] { pids[n] });

        svc.SuspendBackgroundApps();

        suspended.Should().Equal(5);
    }

    // ── Trimming: RAM cleaner, deep clean, adaptive cleanup, game boost ──────────────
    [Theory]
    [InlineData(RamCleanMode.Normal)]
    [InlineData(RamCleanMode.Deep)]
    public async Task Ram_cleaner_never_trims_device_software_or_its_helpers(RamCleanMode mode)
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(new List<ProcessSnapshot>
        {
            new(11, "lghub_agent", 300_000_000, ParentPid: 1),
            new(12, "SignalRgb", 600_000_000, ParentPid: 1),
            new(13, "StreamDeck", 200_000_000, ParentPid: 1),
            new(14, "com.elgato.discord", 120_000_000, ParentPid: 13),   // a Stream Deck plugin
            new(15, "GameSirNexus", 150_000_000, ParentPid: 1),
            new(16, "BackgroundSync", 600_000_000, ParentPid: 1),
        });

        var report = await new SafeRamCleaner(trimmer.Object).RunAsync(0, Array.Empty<int>(), mode);

        report.TrimmedPids.Should().Equal(16);
        foreach (var pid in new[] { 11, 12, 13, 14, 15 })
            trimmer.Verify(t => t.TrimWorkingSet(pid), Times.Never);
    }

    [Fact]
    public async Task Game_boost_never_trims_device_software()
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(new List<ProcessSnapshot>
        {
            new(21, "lghub_agent", 300_000_000, ParentPid: 1),
            new(22, "SignalRgb", 600_000_000, ParentPid: 1),
        });

        await new GameBooster(new SafeRamCleaner(trimmer.Object)).QueueAsync(9999, Array.Empty<int>());

        trimmer.Verify(t => t.TrimWorkingSet(It.IsAny<int>()), Times.Never);
    }

    // ── Priority and memory limits: the engine ───────────────────────────────────────
    [Theory]
    [InlineData(@"C:\Program Files\LGHUB\lghub_agent.exe", "lghub_agent.exe")]
    [InlineData(@"C:\Users\x\AppData\Local\VortxEngine\app-2.4\SignalRgb.exe", "SignalRgb.exe")]
    [InlineData(@"C:\Program Files\Elgato\StreamDeck\StreamDeck.exe", "StreamDeck.exe")]
    [InlineData(@"C:\Program Files\GameSir\GameSirNexus.exe", "GameSirNexus.exe")]
    public async Task Rule_engine_never_lowers_or_caps_device_software_even_when_a_rule_says_so(
        string path, string exe)
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.ImagePathOf(77)).Returns(path);
        var engine = new PriorityRuleEngine(watcher, priority.Object, new Mock<IGameBooster>().Object);
        await engine.ReloadAsync(new[]
        {
            new PriorityRule(path, exe, PriorityLevel.BelowNormal, false, false, false, MemoryLimitMb: 256),
        });
        engine.Start();

        watcher.RaiseStarted(77, exe);

        priority.Verify(p => p.TrySetPriority(77, PriorityLevel.BelowNormal), Times.Never);
        priority.Verify(p => p.TrySetMemoryLimit(77, It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void A_device_rule_may_still_be_raised()
    {
        var rule = new PriorityRule(@"C:\x\lghub_agent.exe", "G HUB", PriorityLevel.High, false, false, false,
            MemoryLimitMb: 512);

        var safe = DeviceSoftware.Sanitize(rule);

        safe.Priority.Should().Be(PriorityLevel.High);
        safe.MemoryLimitMb.Should().BeNull();
    }

    // ── Priority and memory limits: the Memory page ──────────────────────────────────
    [Theory]
    [InlineData(@"C:\Program Files\LGHUB\lghub_agent.exe")]
    [InlineData(@"C:\VortxEngine\SignalRgb.exe")]
    [InlineData(@"C:\Program Files\Elgato\StreamDeck\StreamDeck.exe")]
    [InlineData(@"C:\GameSir\GameSirNexus.exe")]
    public void Memory_page_row_refuses_low_priority_and_limits_for_device_software(string path)
    {
        var vm = new PriorityRuleVm(new PriorityRule(path, "Device", PriorityLevel.Normal, false, false, false));

        vm.Priority = PriorityLevel.BelowNormal;
        vm.MemoryLimit = PriorityRuleVm.MemoryLimitOptions.First(o => o.Mb == 512);

        vm.Priority.Should().Be(PriorityLevel.Normal);
        vm.MemoryLimit!.Mb.Should().BeNull();
        vm.ToRule().Priority.Should().Be(PriorityLevel.Normal);
        vm.ToRule().MemoryLimitMb.Should().BeNull();
    }

    [Fact]
    public void Memory_page_row_loaded_from_an_old_rule_file_comes_back_safe()
    {
        var vm = new PriorityRuleVm(new PriorityRule(@"C:\x\SignalRgb.exe", "SignalRGB",
            PriorityLevel.BelowNormal, false, false, false, MemoryLimitMb: 512, LimitAutoAssigned: true));

        vm.Priority.Should().Be(PriorityLevel.Normal);
        vm.MemoryLimit!.Mb.Should().BeNull();
    }

    [Fact]
    public void Memory_page_row_for_an_ordinary_app_still_takes_low_priority_and_a_limit()
    {
        var vm = new PriorityRuleVm(new PriorityRule(@"C:\x\OneDrive.exe", "OneDrive",
            PriorityLevel.Normal, false, false, false));

        vm.Priority = PriorityLevel.BelowNormal;
        vm.MemoryLimit = PriorityRuleVm.MemoryLimitOptions.First(o => o.Mb == 512);

        vm.ToRule().Priority.Should().Be(PriorityLevel.BelowNormal);
        vm.ToRule().MemoryLimitMb.Should().Be(512);
    }

    // ── Recommended limits ───────────────────────────────────────────────────────────
    [Theory]
    [InlineData(@"C:\x\SignalRgb.exe")]
    [InlineData(@"C:\x\lghub_agent.exe")]
    [InlineData(@"C:\x\StreamDeck.exe")]
    [InlineData(@"C:\x\GameSirNexus.exe")]
    public void Recommended_limits_never_cap_device_software(string path)
    {
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(It.IsAny<string>())).Returns(Array.Empty<int>());

        RecommendedLimits.RecommendMb(path, priority.Object).Should().BeNull();
        RecommendedLimits.KnownAppLimitsMb.Keys.Should().NotContain(k => DeviceSoftware.IsProtected(k));
    }

    // ── Game learning: a device app must never become a "game" ───────────────────────
    [Theory]
    [MemberData(nameof(DeviceApps))]
    public void Device_software_is_never_learned_as_a_game(string name) =>
        AutoGameLearner.CouldBeAGame(name).Should().BeFalse();

    private sealed class TestWatcher : IProcessWatcher
    {
        public event EventHandler<ProcessStartedEvent>? ProcessStarted;
#pragma warning disable CS0067
        public event EventHandler<ProcessStoppedEvent>? ProcessStopped;
#pragma warning restore CS0067
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
        public void RaiseStarted(int pid, string name) => ProcessStarted?.Invoke(this, new ProcessStartedEvent(pid, name));
    }
}
