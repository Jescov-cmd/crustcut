using PrimeOSTuner.Core.Safety;

namespace PrimeOSTuner.Core.Suspension;

/// <summary>One app on the sleep list, as the UI shows it. <paramref name="Reason"/> says
/// why an awake app is awake — a list where nothing happens must say why.</summary>
public sealed record GameOnlyAppStatus(string Name, GameOnlyState State, long MemoryBytes,
    string? Reason = null, bool WakesForGames = true);

public enum GameOnlyState { NotRunning, Awake, Frozen }

/// <summary>
/// The rules for the sleep list. Two kinds of app are on it:
/// - Game companions (clip recorders, overlays, crosshairs, frame-gen tools) only earn
///   their keep while a game runs: they sleep whenever no game does, and wake for games.
/// - Anything else the user added (a slicer, a sync client, a chat app they leave open):
///   it sleeps whenever they aren't using it — through games too — and wakes when they
///   open it.
/// Either way they are FROZEN, never closed: a frozen app keeps its state, logins and tray icon, costs zero CPU, and —
/// because it touches none of its memory — lets Windows hand that memory to whatever the
/// user is actually doing. Unlike trimming a running app, it can't fault it straight back.
///
/// Pure decisions; the service supplies the clock and the process facts.
/// </summary>
public static class GameOnlyPolicy
{
    /// <summary>After a game closes, stay awake long enough to finish work the game
    /// triggered — a clip recorder saving the last clip, above all. Kept short: the point
    /// is that everything goes back to sleep on its own soon after you stop playing.</summary>
    public static readonly TimeSpan AfterGameGrace = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Which apps are game-only right now: the ones the user added, plus every
    /// recommended app that is running — found automatically, no setup — minus the ones
    /// the user took off the list.
    /// </summary>
    public static IReadOnlyList<string> Effective(
        IEnumerable<string> userAdded, IEnumerable<string> excluded, IEnumerable<string> runningNames)
    {
        var skip = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
        var running = new HashSet<string>(runningNames, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in userAdded)
            if (!skip.Contains(n) && CanFreeze(n) && seen.Add(n)) result.Add(n);
        foreach (var n in Recommended)
            if (running.Contains(n) && !skip.Contains(n) && CanFreeze(n) && seen.Add(n)) result.Add(n);
        return result;
    }

    /// <summary>After Crustcut starts, let the machine settle before freezing anything.</summary>
    public static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(2);

    /// <summary>An app the user looked at recently is in use, game or not.</summary>
    public static readonly TimeSpan RecentUseGrace = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Never freezable. Launchers must be awake to START a game (freeze Steam and clicking
    /// Play does nothing); the rest is Windows itself or Crustcut.
    /// </summary>
    private static readonly HashSet<string> Never = new(StringComparer.OrdinalIgnoreCase)
    {
        // launchers
        "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "EADesktop", "EABackgroundService",
        "upc", "UbisoftConnect", "Battle.net", "Agent", "GalaxyClient", "RiotClientServices",
        "RiotClientUx", "Modrinth App", "ModrinthApp", "CurseForge", "PrismLauncher", "MultiMC",
        "MinecraftLauncher", "XboxPcApp", "GamingServices",
        // Windows and the shell
        "explorer", "dwm", "csrss", "winlogon", "wininit", "services", "lsass", "svchost",
        "sihost", "taskhostw", "RuntimeBroker", "ShellExperienceHost", "StartMenuExperienceHost",
        "SearchHost", "TextInputHost", "ctfmon", "audiodg", "fontdrvhost", "System", "Registry",
        "Memory Compression", "smss", "conhost", "dllhost",
        // Voice and chat: a frozen one is a dead microphone mid-call.
        "Discord", "TeamSpeak", "ts3client_win64", "Mumble", "Teams", "ms-teams", "Zoom",
        // Recording, and tools that act on a key press system-wide.
        "QuickLook", "obs64", "obs32",
        // Hardware companion software (mice, keyboards, lighting, controllers, audio
        // drivers, GPU panels) is NOT listed here: it lives in DeviceSoftware, the one list
        // every feature that freezes, trims, caps or deprioritises asks. CanFreeze checks it.
        // ourselves
        "Crustcut",
    };

    /// <summary>Well-known apps that are only useful in games — offered first in the UI.</summary>
    public static readonly IReadOnlyList<string> Recommended = new[]
    {
        "Medal", "MedalEncoder", "Outplayed", "Overwolf",
        "LosslessScaling", "CrosshairX", "RTSS", "RazerCortex", "Blitz",
    };

    public static bool CanFreeze(string name) =>
        !Never.Contains(DeviceSoftware.Normalize(name)) && !DeviceSoftware.IsProtected(name);

    /// <summary>A game companion wakes for games; anything else stays asleep through them.</summary>
    public static bool WakesForGames(string name) =>
        Recommended.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Should this app be frozen right now?</summary>
    /// <param name="hasWindowOnScreen">Any of its processes has a restored, visible window.</param>
    /// <param name="lastForegroundUtc">When the user last had one of its windows in front.</param>
    public static bool ShouldFreeze(
        string name, DateTime nowUtc, bool enabled, bool gameRunning,
        DateTime lastGameEndedUtc, DateTime serviceStartedUtc,
        bool hasWindowOnScreen, DateTime lastForegroundUtc)
        => WhyAwake(name, nowUtc, enabled, gameRunning, lastGameEndedUtc, serviceStartedUtc,
            hasWindowOnScreen, lastForegroundUtc) is null;

    /// <summary>Null when the app should be frozen now; otherwise the reason it stays
    /// awake, in words the UI can show.</summary>
    public static string? WhyAwake(
        string name, DateTime nowUtc, bool enabled, bool gameRunning,
        DateTime lastGameEndedUtc, DateTime serviceStartedUtc,
        bool hasWindowOnScreen, DateTime lastForegroundUtc)
    {
        if (!enabled) return "the switch is off";
        if (!CanFreeze(name)) return "never put to sleep";
        var forGames = WakesForGames(name);
        if (forGames && gameRunning) return "a game is running";
        if (nowUtc - serviceStartedUtc < StartupGrace) return "Crustcut just started";
        if (forGames && nowUtc - lastGameEndedUtc < AfterGameGrace) return "a game just ended";
        if (hasWindowOnScreen) return "its window is open";
        var idle = nowUtc - lastForegroundUtc;
        if (idle < RecentUseGrace)
            return $"used {Math.Max(1, (int)Math.Ceiling(idle.TotalMinutes))} min ago";
        return null;
    }
}
