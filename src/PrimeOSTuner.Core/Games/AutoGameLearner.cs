using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Games;

/// <summary>The window in front: whose it is, and how much of its monitor it covers (0-1).</summary>
public sealed record ForegroundWindow(int Pid, double ScreenCoverage);

/// <summary>What Crustcut can learn about a process to name it as a game.</summary>
public sealed record ProcessIdentity(string ExeName, string FullPath, string? ProductName);

/// <summary>
/// Finds games Crustcut has never heard of. The library scanners only know Steam, Xbox,
/// Epic, EA, Ubisoft, GOG and a short built-in list; anything else — a standalone
/// download, an itch.io game, a private server client — was invisible: no session, no
/// stutter report, no game-only wake-up. A game is recognised by what it does: its
/// window fills the screen (or most of it) and it keeps the graphics card busy, for long
/// enough that it isn't a flash of animation. Once found it's added to the Games list
/// like a hand-added game, so every later launch is caught instantly by name.
///
/// Pure logic: the foreground window, GPU use and process details are passed in.
/// </summary>
public sealed class AutoGameLearner
{
    // Fullscreen/borderless: a modest, sustained GPU load is enough (light games count).
    private const double FullscreenCoverage = 0.98;
    private const double FullscreenGpuPercent = 15;
    private static readonly TimeSpan FullscreenSustain = TimeSpan.FromSeconds(10);
    // Windowed: demand more, for longer — a big window is weaker evidence than a full screen.
    private const double WindowedCoverage = 0.5;
    private const double WindowedGpuPercent = 30;
    private static readonly TimeSpan WindowedSustain = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Things that fill the screen and use the GPU without being games. Java is here
    /// because Minecraft has its own detector and "javaw" would otherwise teach Crustcut
    /// that every Java program is a game.
    /// </summary>
    private static readonly HashSet<string> NotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "arc", "iexplore",
        "vlc", "mpc-hc64", "mpc-hc", "mpc-be64", "PotPlayerMini64", "PotPlayer64", "mpv", "wmplayer",
        "Video.UI", "Microsoft.Media.Player", "Stremio", "Plex", "Netflix", "Spotify",
        "obs64", "obs32", "Medal", "MedalEncoder", "LosslessScaling", "wallpaper32", "wallpaper64",
        "lively", "Rainmeter", "Code", "devenv", "rider64", "idea64", "pycharm64", "blender",
        "Resolve", "Adobe Premiere Pro", "AfterFX", "Photoshop", "UnityHub", "Unity", "UnrealEditor",
        "Discord", "Teams", "ms-teams", "Zoom", "slack", "explorer", "dwm", "ApplicationFrameHost",
        "TextInputHost", "SearchHost", "LockApp", "ShellExperienceHost", "StartMenuExperienceHost",
        "Crustcut", "javaw", "java", "steam", "steamwebhelper", "EpicGamesLauncher", "Bloxstrap",
        "RobloxStudioBeta", "XboxGameBar", "GameBar", "NVIDIA Overlay", "PresentMon-x64",
        "mstsc", "vmware", "VirtualBoxVM", "vmconnect", "parsecd", "sunshine", "moonlight",
        "CrosshairX", "cursorqt", "Steam360VideoPlayer", "vrmonitor", "vrcompositor",
    };

    private readonly Func<ForegroundWindow?> _foreground;
    private readonly IGpuUsageByProcess _gpu;
    private readonly Func<int, ProcessIdentity?> _identify;
    private readonly Func<IReadOnlySet<string>> _knownExes;
    private readonly Func<KnownGame, Task> _learn;
    private readonly Action<string>? _log;
    private readonly string _windowsDir;
    private readonly HashSet<string> _decided = new(StringComparer.OrdinalIgnoreCase);

    private int _candidatePid;
    private DateTime _streakStartUtc;
    private bool _streakFullscreen;
    private int _busy;
    private int _consideredPid;   // the pid a verdict was already reached for

    public AutoGameLearner(Func<ForegroundWindow?> foreground, IGpuUsageByProcess gpu,
        Func<int, ProcessIdentity?> identify, Func<IReadOnlySet<string>> knownExes,
        Func<KnownGame, Task> learn, Action<string>? log = null, string? windowsDir = null)
    {
        _foreground = foreground;
        _gpu = gpu;
        _identify = identify;
        _knownExes = knownExes;
        _learn = learn;
        _log = log;
        _windowsDir = windowsDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }

    public static bool CouldBeAGame(string exeNameWithoutExtension) => !NotGames.Contains(exeNameWithoutExtension);

    /// <summary>Called once per system sample (about every second).</summary>
    public void OnSample(DateTime nowUtc)
    {
        var fg = _foreground();
        var gpu = fg is null ? 0 : _gpu.GpuPercentByPid.GetValueOrDefault(fg.Pid);
        var fullscreen = fg is { ScreenCoverage: >= FullscreenCoverage } && gpu >= FullscreenGpuPercent;
        var windowed = fg is { ScreenCoverage: >= WindowedCoverage } && gpu >= WindowedGpuPercent;

        if (fg is null || fg.Pid == Environment.ProcessId || (!fullscreen && !windowed))
        {
            _candidatePid = 0;
            return;
        }
        if (fg.Pid != _candidatePid || fullscreen != _streakFullscreen)
        {
            _candidatePid = fg.Pid;
            _streakStartUtc = nowUtc;
            _streakFullscreen = fullscreen;
            return;
        }
        if (nowUtc - _streakStartUtc < (fullscreen ? FullscreenSustain : WindowedSustain)) return;

        // Sustained. Decide once per process (and once per exe), off the sampler's thread.
        if (fg.Pid == _consideredPid) return;
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        var pid = fg.Pid;
        _consideredPid = pid;
        _ = Task.Run(async () =>
        {
            try { await ConsiderAsync(pid); }
            catch (Exception ex) { _log?.Invoke($"games: couldn't add an automatically found game: {ex.Message}"); }
            finally { Volatile.Write(ref _busy, 0); }
        });
    }

    private async Task ConsiderAsync(int pid)
    {
        var who = _identify(pid);
        if (who is null) return;
        var name = Path.GetFileNameWithoutExtension(who.ExeName);
        if (!_decided.Add(name)) return;                                   // one verdict per exe
        if (!CouldBeAGame(name)) return;
        if (who.FullPath.StartsWith(_windowsDir, StringComparison.OrdinalIgnoreCase)) return;
        var exe = name + ".exe";
        if (_knownExes().Contains(exe)) return;                            // already a known game

        var display = string.IsNullOrWhiteSpace(who.ProductName) ? name : who.ProductName!.Trim();
        var game = new KnownGame(
            Id: "auto." + name.ToLowerInvariant(),
            DisplayName: display,
            ExecutableNames: new[] { exe },
            SteamAppId: null,
            InstallPath: Path.GetDirectoryName(who.FullPath),
            Source: KnownGameSource.UserAdded);
        await _learn(game);
        _log?.Invoke($"games: found {display} automatically ({exe}) — added to your Games list; it's measured from now on");
    }
}
