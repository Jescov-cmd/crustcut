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
}
