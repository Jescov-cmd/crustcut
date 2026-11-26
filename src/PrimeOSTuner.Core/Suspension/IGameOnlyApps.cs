namespace PrimeOSTuner.Core.Suspension;

/// <summary>The game-only apps feature as the UI sees it. See <see cref="GameOnlyPolicy"/>.</summary>
public interface IGameOnlyApps
{
    bool Enabled { get; set; }

    /// <summary>Every listed app with its live state.</summary>
    IReadOnlyList<GameOnlyAppStatus> Status();

    /// <summary>Running apps that could be added, recommended ones first.</summary>
    IReadOnlyList<string> Candidates();

    void Add(string name);
    void Remove(string name);   // also wakes it

    /// <summary>Thaws an app now and gives it the recent-use grace before it can refreeze.</summary>
    void Wake(string name);

    /// <summary>Raised when an app freezes or wakes, or the list changes.</summary>
    event EventHandler? Changed;
}
