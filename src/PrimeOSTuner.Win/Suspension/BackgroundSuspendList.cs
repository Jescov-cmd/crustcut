namespace PrimeOSTuner.Win.Suspension;

/// <summary>
/// The default curated set of background apps that are safe to suspend during
/// a game session. Names match the <see cref="System.Diagnostics.Process.ProcessName"/>
/// form (no .exe).
///
/// Deliberately conservative: only clear-cut cloud-sync clients, media apps,
/// and the Epic launcher. Steam is NOT here even though the brief mentions it,
/// because Steam needs to be alive to launch Steam-bound games — that goes
/// through the lifecycle layer with timing, not the default list. Browsers are
/// excluded because their child-process trees are too messy to freeze safely
/// by name.
/// </summary>
public static class BackgroundSuspendList
{
    // Curation rule: only apps whose PAUSE is invisible during gameplay. RGB and
    // wallpaper software just stops animating; sync clients stop syncing; launchers stop
    // updating. Deliberately absent: voice/audio tools (WaveLink, Discord — freezing them
    // kills the user's microphone mid-game), capture tools (Medal — people want their
    // clips), and macro/input software people actively use in-game (Stream Deck).
    public static readonly IReadOnlyList<string> Default = new[]
    {
        "OneDrive",
        "Dropbox",
        "googledrivesync",
        "GoogleDriveFS",
        "Spotify",
        "EpicGamesLauncher",
        "SignalRgb",
        "SignalRgbLauncher",
        "lghub",
        "lghub_agent",
        "lghub_updater",
        "wallpaper32",
        "wallpaper64",
        "Overwolf",
        "CurseForge",
        "GalaxyClient",
        "EADesktop",
    };
}
