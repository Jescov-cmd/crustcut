using FluentAssertions;
using PrimeOSTuner.Core.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Suspension;

public class GameOnlyPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LongAgo = Now.AddHours(-1);

    private static bool Freeze(string name = "Medal", bool enabled = true, bool gameRunning = false,
        DateTime? lastGameEnded = null, DateTime? serviceStarted = null,
        bool windowOnScreen = false, DateTime? lastForeground = null) =>
        GameOnlyPolicy.ShouldFreeze(name, Now, enabled, gameRunning,
            lastGameEnded ?? LongAgo, serviceStarted ?? LongAgo, windowOnScreen, lastForeground ?? LongAgo);

    [Fact]
    public void Idle_game_only_app_with_no_game_running_is_frozen() => Freeze().Should().BeTrue();

    [Fact]
    public void Nothing_freezes_while_switched_off() => Freeze(enabled: false).Should().BeFalse();

    [Fact]
    public void Nothing_freezes_while_a_game_runs() => Freeze(gameRunning: true).Should().BeFalse();

    [Fact]
    public void Stays_awake_briefly_after_a_game_then_sleeps_on_its_own()
    {
        Freeze(lastGameEnded: Now.AddSeconds(-30)).Should().BeFalse();   // last clip still saving
        Freeze(lastGameEnded: Now.AddMinutes(-1)).Should().BeTrue();     // back asleep, no action needed
    }

    /// <summary>An app the user added that isn't a game companion (a slicer, a sync
    /// client) sleeps whenever it's unused — through games too, when the game wants the memory.</summary>
    [Fact]
    public void An_added_everyday_app_sleeps_when_unused_even_during_a_game()
    {
        Freeze("bambu-studio", gameRunning: true).Should().BeTrue();
        Freeze("bambu-studio", lastGameEnded: Now.AddSeconds(-10)).Should().BeTrue();
        Freeze("bambu-studio", windowOnScreen: true).Should().BeFalse();
        Freeze("bambu-studio", lastForeground: Now.AddMinutes(-1)).Should().BeFalse();
    }

    [Fact]
    public void Only_game_companions_wake_for_games()
    {
        GameOnlyPolicy.WakesForGames("Medal").Should().BeTrue();
        GameOnlyPolicy.WakesForGames("bambu-studio").Should().BeFalse();
    }

    /// <summary>The user, 2026-10-01: "they're not going to sleep" — with no reason shown,
    /// an awake app looks like a feature that does nothing. Every awake verdict says why.</summary>
    [Fact]
    public void An_awake_app_always_says_why()
    {
        string? Why(string name = "Medal", bool enabled = true, bool gameRunning = false,
            DateTime? lastGameEnded = null, DateTime? serviceStarted = null,
            bool windowOnScreen = false, DateTime? lastForeground = null) =>
            GameOnlyPolicy.WhyAwake(name, Now, enabled, gameRunning,
                lastGameEnded ?? LongAgo, serviceStarted ?? LongAgo, windowOnScreen, lastForeground ?? LongAgo);

        Why().Should().BeNull();
        Why(enabled: false).Should().Be("the switch is off");
        Why(gameRunning: true).Should().Be("a game is running");
        Why(serviceStarted: Now.AddSeconds(-20)).Should().Be("Crustcut just started");
        Why(lastGameEnded: Now.AddSeconds(-20)).Should().Be("a game just ended");
        Why(windowOnScreen: true).Should().Be("its window is open");
        Why(lastForeground: Now.AddSeconds(-90)).Should().Be("used 2 min ago");
        Why("lghub_agent").Should().Be("never put to sleep");
    }

    [Fact]
    public void Known_game_apps_are_found_automatically_when_running()
    {
        GameOnlyPolicy.Effective(
                userAdded: Array.Empty<string>(), excluded: Array.Empty<string>(),
                runningNames: new[] { "Medal", "chrome", "Code", "LosslessScaling" })
            .Should().BeEquivalentTo("Medal", "LosslessScaling");
    }

    [Fact]
    public void An_app_the_user_took_off_stays_off_and_user_added_ones_stay_on()
    {
        GameOnlyPolicy.Effective(
                userAdded: new[] { "MyOverlay" }, excluded: new[] { "Medal" },
                runningNames: new[] { "Medal", "Overwolf" })
            .Should().BeEquivalentTo("MyOverlay", "Overwolf");
    }

    [Fact]
    public void A_launcher_added_by_hand_is_still_never_used()
    {
        GameOnlyPolicy.Effective(new[] { "steam" }, Array.Empty<string>(), new[] { "steam" })
            .Should().BeEmpty();
    }

    [Fact]
    public void Nothing_freezes_right_after_crustcut_starts() =>
        Freeze(serviceStarted: Now.AddSeconds(-20)).Should().BeFalse();

    [Fact]
    public void An_app_on_screen_or_used_recently_is_left_alone()
    {
        Freeze(windowOnScreen: true).Should().BeFalse();
        Freeze(lastForeground: Now.AddMinutes(-1)).Should().BeFalse();
    }

    /// <summary>A frozen launcher can't start the game — clicking Play would do nothing.</summary>
    [Theory]
    [InlineData("steam")]
    [InlineData("EpicGamesLauncher")]
    [InlineData("Modrinth App")]
    [InlineData("explorer")]
    [InlineData("Crustcut")]
    // The first live run froze all four of these: microphone, macro keys, mouse, desktop mods.
    [InlineData("ElgatoAudioControlServer")]
    [InlineData("StreamDeck")]
    [InlineData("lghub_agent")]
    [InlineData("windhawk")]
    [InlineData("Discord")]
    public void Launchers_input_audio_windows_and_crustcut_are_never_frozen(string name)
    {
        GameOnlyPolicy.CanFreeze(name).Should().BeFalse();
        Freeze(name).Should().BeFalse();
    }
}
