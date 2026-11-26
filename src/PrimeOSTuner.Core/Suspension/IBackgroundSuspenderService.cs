namespace PrimeOSTuner.Win.Suspension;

public sealed record SuspendedProcessInfo(int Pid, string Name);

/// <summary>
/// Freezes a curated set of background apps (cloud sync clients, media apps,
/// launchers) while a game is running, and resumes them when it exits.
///
/// Stateful: keeps a record of what it actually suspended so resume targets
/// only those pids. Lets the UI list "currently suspended" processes.
/// </summary>
public interface IBackgroundSuspenderService
{
    IReadOnlyList<SuspendedProcessInfo> Currently { get; }
    event EventHandler? Changed;

    IReadOnlyList<SuspendedProcessInfo> SuspendBackgroundApps();
    void ResumeAll();

    /// <summary>Wakes one paused app early (the user brought it to the front mid-game).
    /// It stays awake for the rest of the game.</summary>
    void Resume(string name) { }
}
