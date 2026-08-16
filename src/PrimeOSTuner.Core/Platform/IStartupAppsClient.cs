namespace PrimeOSTuner.Core.Platform;

/// <summary>One program that launches with Windows.</summary>
/// <param name="Name">Registry value name or shortcut filename — the toggle key.</param>
/// <param name="Command">What actually runs, for display.</param>
/// <param name="Source">Where it's registered (current user, all users, Startup folder).</param>
/// <param name="Enabled">False when Windows has it marked disabled (Task Manager style).</param>
public sealed record StartupApp(string Name, string Command, StartupSource Source, bool Enabled);

public enum StartupSource { CurrentUser, AllUsers, StartupFolder }

/// <summary>
/// Startup program enumeration and enable/disable. Disabling uses the SAME mechanism as
/// Task Manager (the StartupApproved registry marks), so nothing is deleted, Windows'
/// own UI shows the same state, and re-enabling is one click from either app.
/// </summary>
public interface IStartupAppsClient
{
    IReadOnlyList<StartupApp> List();
    bool TrySetEnabled(StartupApp app, bool enabled);
}
