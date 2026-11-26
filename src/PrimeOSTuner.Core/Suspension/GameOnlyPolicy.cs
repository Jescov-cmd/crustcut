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
            if (running.Contains(n) && !skip.Contains(n) && seen.Add(n)) result.Add(n);
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
        // Things the user is plugged into all day, game or not. Freezing these doesn't
        // save memory, it breaks the PC: a frozen audio server is a dead microphone, a
        // frozen G HUB agent loses mouse buttons and DPI, a frozen Stream Deck stops
        // every key. Learned the hard way — the first live test froze exactly these.
        "ElgatoAudioControlServer", "WaveLink", "WaveLinkSE", "Elgato Wave Link", "Camera Hub",
        "voicemeeter", "voicemeeterpro", "voicemeeter8", "voicemeeter8x64", "audiorelay",
        "Discord", "TeamSpeak", "ts3client_win64", "Mumble", "Teams", "ms-teams", "Zoom",
        // Hardware companion software: mice, keyboards, headsets, lighting, cooling. These
        // drive physical devices — frozen, a mouse loses its DPI and buttons, RGB stops,
        // an AIO's pump curve stalls. (The user, 2026-10-01: "very important for physical
        // items".) Never frozen by any feature.
        "StreamDeck", "lghub", "lghub_agent", "lghub_updater", "lghub_system_tray",
        "logioptionsplus_agent", "logioptionsplus_appbroker", "LogiOptionsPlus", "LogiOptions",
        "RazerAppEngine", "Razer Synapse", "Razer Synapse 3", "RazerCentralService",
        "iCUE", "Corsair.Service", "SteelSeriesEngine", "SteelSeriesGG", "SteelSeriesGGClient",
        "SteelSeriesPrism", "WootilityV5", "Wootility", "NGenuity2", "HyperX NGENUITY",
        "GloriousCore", "ROCCAT_Swarm", "SignalRgb", "SignalRgbLauncher", "OpenRGB",
        "ArmouryCrate", "ArmouryCrate.Service", "ArmouryCrateControlInterface", "LightingService",
        "MSI.CentralServer", "MSI Center", "MysticLight", "MSI_Central_Service", "NZXT CAM", "CAM",
        "RtkAudUService64", "RtkNGUI64", "NahimicService", "Nahimic3", "MaxxAudioPro",
        "MSIAfterburner",   // runs the graphics card's fan curve and clocks
        // Tools that act on key presses or mouse buttons system-wide. Frozen, a
        // keyboard/mouse hook stalls every key and click until Windows drops it — and
        // then the tool silently stops working until it restarts.
        "FluentFlyout", "XMouseButtonControl", "QuickLook",
        "windhawk", "AutoHotkey", "AutoHotkey64", "AutoHotkeyU64", "PowerToys",
        "PowerToys.KeyboardManagerEngine", "PowerToys.FancyZones", "obs64", "obs32",
        // ourselves
        "Crustcut",
    };

    /// <summary>Well-known apps that are only useful in games — offered first in the UI.</summary>
    public static readonly IReadOnlyList<string> Recommended = new[]
    {
        "Medal", "MedalEncoder", "Outplayed", "Overwolf",
        "LosslessScaling", "CrosshairX", "RTSS", "RazerCortex", "Blitz",
    };

    public static bool CanFreeze(string name) => !Never.Contains(name);

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
