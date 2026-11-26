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
    // Curation rule: only apps whose PAUSE is invisible during gameplay. Sync clients stop
    // syncing, wallpapers stop animating, companion and helper apps stop doing busywork —
    // and Windows can hand their memory to the game. All of it wakes when the game closes,
    // or the moment the user brings one to the front.
    //
    // Never on this list (the shared never-freeze list in GameOnlyPolicy also blocks them
    // at the service level, whatever list a profile passes in):
    // - hardware companion software — G HUB, SignalRGB, iCUE, Synapse: mouse DPI and
    //   buttons, lighting, fans. Freezing G HUB here is what the user hit on 2026-10-01.
    // - voice and audio — Discord, Wave Link: a frozen one is a dead microphone.
    // - music players — Spotify was here until 2026-10-01; a frozen player is silence.
    // - capture and overlays — Medal, Overwolf: those are the sleep list's, awake in games.
    // - launchers — Steam, Epic, EA, GOG: games need them running.
    // - anything that hooks keys or mouse buttons system-wide.
    public static readonly IReadOnlyList<string> Default = new[]
    {
        // cloud sync
        "OneDrive", "Dropbox", "googledrivesync", "GoogleDriveFS",
        "iCloudDrive", "iCloudServices", "iCloudPhotos", "iCloudHome", "iCloud",
        "MEGAsync", "pCloud", "nextcloud", "Box",
        // animated wallpapers
        "wallpaper32", "wallpaper64", "Lively",
        // phone link and widgets
        "PhoneExperienceHost", "Widgets",
        // creative-suite background helpers (not the apps themselves)
        "Creative Cloud", "CCXProcess", "CCLibrary", "Adobe Desktop Service", "CoreSync",
        // notes and docs left open in the background
        "Notion", "Evernote",
    };
}
