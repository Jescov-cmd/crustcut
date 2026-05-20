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
    public static readonly IReadOnlyList<string> Default = new[]
    {
        "OneDrive",
        "Dropbox",
        "googledrivesync",
        "GoogleDriveFS",
        "Spotify",
        "EpicGamesLauncher",
    };
}
