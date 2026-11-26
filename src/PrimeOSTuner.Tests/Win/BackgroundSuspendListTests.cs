using FluentAssertions;
using PrimeOSTuner.Win.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

public class BackgroundSuspendListTests
{
    [Fact]
    public void Default_includes_common_cloud_sync_apps()
    {
        BackgroundSuspendList.Default.Should().Contain(new[]
        {
            "OneDrive", "Dropbox", "iCloudDrive",
        });
    }

    /// <summary>Pausing a music player mid-game is silence, not savings.</summary>
    [Fact]
    public void Default_leaves_music_playing()
    {
        BackgroundSuspendList.Default.Should().NotContain("Spotify");
    }

    /// <summary>Every default entry must actually be pausable — a list full of names the
    /// service then refuses is a list that looks like it does more than it does.</summary>
    [Fact]
    public void Every_default_entry_is_actually_pausable()
    {
        BackgroundSuspendList.Default.Should().OnlyContain(n =>
            PrimeOSTuner.Core.Suspension.GameOnlyPolicy.CanFreeze(n)
            && !PrimeOSTuner.Core.Suspension.GameOnlyPolicy.WakesForGames(n));
    }

    [Fact]
    public void Default_excludes_Steam_because_it_must_be_running_to_launch_games()
    {
        BackgroundSuspendList.Default.Should().NotContain(n =>
            string.Equals(n, "Steam", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Default_excludes_browsers_because_their_child_process_trees_are_too_messy()
    {
        BackgroundSuspendList.Default.Should().NotContain(n =>
            string.Equals(n, "chrome", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "msedge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "firefox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Default_entries_have_no_exe_extension()
    {
        BackgroundSuspendList.Default.Should().AllSatisfy(n =>
            n.Should().NotEndWith(".exe"));
    }

    /// <summary>The user, 2026-10-01: pausing for a game froze Logitech G HUB. Software
    /// that drives physical devices must never be on the list.</summary>
    [Theory]
    [InlineData("lghub")]
    [InlineData("lghub_agent")]
    [InlineData("SignalRgb")]
    public void Hardware_companion_software_is_never_paused(string name)
    {
        BackgroundSuspendList.Default.Should().NotContain(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        PrimeOSTuner.Core.Suspension.GameOnlyPolicy.CanFreeze(name).Should().BeFalse();
    }

    [Fact]
    public void A_profile_list_that_names_hardware_software_still_skips_it()
    {
        var suspended = new List<int>();
        var suspender = new Moq.Mock<IProcessSuspender>();
        suspender.Setup(s => s.Suspend(Moq.It.IsAny<int>())).Callback<int>(suspended.Add);
        var svc = new BackgroundSuspenderService(suspender.Object,
            targetNames: new[] { "lghub_agent", "Spotify" },
            processIdsByName: n => n == "lghub_agent" ? new[] { 11 } : new[] { 22 });

        svc.SuspendBackgroundApps();

        suspended.Should().Equal(22);
    }

    [Fact]
    public void Apps_the_sleep_list_owns_are_never_paused_twice()
    {
        var suspended = new List<int>();
        var suspender = new Moq.Mock<IProcessSuspender>();
        suspender.Setup(s => s.Suspend(Moq.It.IsAny<int>())).Callback<int>(suspended.Add);
        var svc = new BackgroundSuspenderService(suspender.Object,
            targetNames: new[] { "OneDrive", "Dropbox", "Medal" },
            processIdsByName: n => n switch { "OneDrive" => new[] { 1 }, "Dropbox" => new[] { 2 }, _ => new[] { 3 } },
            ownedElsewhere: () => new[] { "dropbox" });

        svc.SuspendBackgroundApps();

        suspended.Should().Equal(1);   // Dropbox belongs to the sleep list, Medal wakes for games
    }

    [Fact]
    public void An_app_woken_mid_game_stays_awake_until_the_game_ends()
    {
        var suspended = new List<int>();
        var resumed = new List<int>();
        var suspender = new Moq.Mock<IProcessSuspender>();
        suspender.Setup(s => s.Suspend(Moq.It.IsAny<int>())).Callback<int>(suspended.Add);
        suspender.Setup(s => s.Resume(Moq.It.IsAny<int>())).Callback<int>(resumed.Add);
        var svc = new BackgroundSuspenderService(suspender.Object,
            targetNames: new[] { "OneDrive" }, processIdsByName: _ => new[] { 7 });

        svc.SuspendBackgroundApps();
        svc.Resume("onedrive");
        svc.SuspendBackgroundApps();   // a profile applying again mid-game

        suspended.Should().Equal(7);
        resumed.Should().Equal(7);
        svc.Currently.Should().BeEmpty();

        svc.ResumeAll();               // game over: next game pauses it again
        svc.SuspendBackgroundApps();
        suspended.Should().Equal(7, 7);
    }

    [Fact]
    public void Default_entries_are_unique()
    {
        BackgroundSuspendList.Default.Should().OnlyHaveUniqueItems();
    }
}
