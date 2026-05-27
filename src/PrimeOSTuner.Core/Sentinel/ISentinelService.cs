using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Sentinel;

public interface ISentinelService
{
    /// <summary>Problems currently detected; empty when no game is running or no rule fires.</summary>
    IReadOnlyList<Problem> Currently { get; }

    /// <summary>Display name of the game being watched, or null when idle.</summary>
    string? WatchingGame { get; }

    /// <summary>Most recent metrics sample, or null if the loop hasn't ticked yet.</summary>
    MetricsSnapshot? LatestSnapshot { get; }

    /// <summary>Spec for the currently-watched game, or null if not yet fetched / unavailable.</summary>
    SteamPcRequirements? CurrentSpec { get; }

    /// <summary>Master switch. When false, OnGameStarted no-ops and any in-flight loop stops on next tick.</summary>
    bool Enabled { get; set; }

    /// <summary>Raised whenever Currently, WatchingGame, LatestSnapshot, or CurrentSpec changes.</summary>
    event EventHandler? Changed;

    /// <summary>Start watching a game. Idempotent — safe to call multiple times.</summary>
    void OnGameStarted(KnownGame game, int pid);

    /// <summary>Stop watching. Safe to call when not watching.</summary>
    void OnGameStopped();
}
