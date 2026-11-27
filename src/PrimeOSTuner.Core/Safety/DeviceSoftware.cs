using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Safety;

/// <summary>
/// Software that drives physical devices or hooks input and audio system-wide: mice and
/// keyboards, lighting, controllers, macro pads, microphones and mixers, GPU control panels.
///
/// The rule (the user, 2026-11-26): Crustcut never freezes, suspends, sleeps, kills,
/// lowers the priority of, memory-limits or trims any of these — not automatically, not
/// from a profile, not because a user rule asks. Frozen, G HUB loses mouse DPI and buttons
/// and a Stream Deck stops every key; trimmed or capped, they page back in on the next
/// key press and the input hitches; deprioritised, a busy game starves them.
///
/// This is the ONE list. The sleep list, pause-while-gaming, Game mode, the RAM cleaner
/// (normal, deep, adaptive, game boost), the Memory page's priority and limit rules and the
/// game learner all ask <see cref="IsProtected"/>; the OS-call layer (suspender, trimmer,
/// priority client) asks it again as a last line of defence.
///
/// Names are process names as Windows reports them: case-insensitive, with or without
/// ".exe" or a folder in front. A prefix entry covers a vendor's whole family of helper
/// processes (PowerToys ships a dozen, G HUB and iCUE several), so a new helper in an
/// update is protected the day it appears.
/// </summary>
public static class DeviceSoftware
{
    private static readonly HashSet<string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        // Logitech
        "lghub", "lghub_agent", "lghub_updater", "lghub_system_tray",
        "LogiOptionsPlus", "logioptionsplus_agent", "logioptionsplus_appbroker",
        "logioptionsplus_updater", "LogiOptions", "LogiOptionsMgr", "LogiBolt",
        "logi_lamparray_service", "logi_lamparray_service.AMD64",
        // Razer
        "RazerAppEngine", "Razer Synapse", "Razer Synapse 3", "Razer Synapse Service",
        "Razer Synapse Service Process", "RazerCentralService", "Razer Central",
        "RzSDKServer", "RzSDKService", "RzChromaStreamServer", "RzEngineMon",
        // Corsair
        "iCUE", "iCUEDevicePluginHost", "Corsair.Service", "Corsair.Service.CpuIdRemote64",
        "Corsair.Service.DisplayAdapter", "CueLLAccessService", "CorsairDeviceControlService",
        // SteelSeries
        "SteelSeriesEngine", "SteelSeriesGG", "SteelSeriesGGClient", "SteelSeriesPrism",
        "SteelSeriesSonar",
        // Lighting
        "SignalRgb", "SignalRgbLauncher", "OpenRGB",
        // NZXT
        "NZXT CAM", "CAM",
        // ASUS
        "ArmouryCrate", "ArmouryCrate.Service", "ArmouryCrate.UserSessionHelper",
        "ArmouryCrateControlInterface", "ArmourySocketServer", "ArmourySwAgent",
        "AsusCertService", "LightingService", "ROGLiveService", "AuraWallpaperService",
        "asus_framework",
        // MSI
        "MSI.CentralServer", "MSI Center", "MSI_Central_Service", "MysticLight",
        "MSIAfterburner",
        // Elgato: Stream Deck, Wave Link, the Volume Controller audio server, Camera Hub
        "StreamDeck", "WaveLink", "WaveLinkSE", "Elgato Wave Link", "Elgato.WaveLink",
        "WavelinkSEService", "ElgatoAudioControlServer", "ElgatoAudioControlServerWatcher",
        "Camera Hub", "Elgato Control Center",
        // Controllers and keyboards
        "GameSir Nexus", "GameSirNexus", "GameSir-Nexus", "GameSir_Nexus",
        "WootilityV5", "Wootility", "Wootility-lekker", "NGenuity2", "HyperX NGENUITY",
        "GloriousCore", "ROCCAT_Swarm",
        // Audio drivers, panels and voice tools
        "RtkAudUService64", "RtkNGUI64", "RtkUWP", "RtkAudioService64", "RAVBg64", "RAVCpl64",
        "NahimicService", "Nahimic3", "NahimicSvc64", "NahimicSvc32", "MaxxAudioPro",
        "WavesSvc64", "WavesSysSvc64", "DolbyDAX2API", "DolbyDAX3API", "DAX3API", "DolbyAccess",
        "voicemeeter", "voicemeeterpro", "voicemeeter8", "voicemeeter8x64",
        "voicemeeter_x64", "voicemeeterpro_x64", "audiorelay",
        "Voicemod", "VoicemodDesktop", "voicemodcon",
        // System-wide input hooks and shell mods
        "windhawk", "windhawk-x64-helper", "AutoHotkey", "AutoHotkey64", "AutoHotkeyU64",
        "AutoHotkeyU32", "AutoHotkey32", "PowerToys", "TranslucentTB",
        "XMouseButtonControl", "FluentFlyout",
        // GPU control panels
        "NVIDIA app", "NVIDIA App", "nvcontainer", "NVDisplay.Container", "nvcplui",
        "NVIDIA Share", "NVIDIA Overlay", "NVIDIA Web Helper", "nvsphelper64",
        "RadeonSoftware", "Radeon Software", "AMDRSServ", "AMDRSSrcExt", "amdow",
        "atieclxx", "atiesrxx", "cncmd", "amddvr",
    };

    /// <summary>Vendor families whose helper processes come and go between versions.</summary>
    private static readonly string[] Prefixes =
    {
        "lghub", "logioptionsplus", "logi_lamparray", "Razer Synapse", "RzChroma",
        "iCUE", "Corsair.Service", "SteelSeries", "SignalRgb", "ArmouryCrate", "Wootility",
        "StreamDeck", "Elgato", "WaveLink", "GameSir", "Nahimic", "voicemeeter", "Voicemod",
        "windhawk", "AutoHotkey", "PowerToys", "TranslucentTB", "NVIDIA app", "NVDisplay.Container",
    };

    /// <summary>Every exact name, for tests and the UI.</summary>
    public static IReadOnlyCollection<string> Names => Exact;

    /// <summary>
    /// True when <paramref name="processName"/> is device software. Accepts a bare process
    /// name ("lghub_agent"), a file name ("lghub_agent.exe") or a full path.
    /// </summary>
    public static bool IsProtected(string? processName)
    {
        var name = Normalize(processName);
        if (name.Length == 0) return false;
        if (Exact.Contains(name)) return true;
        foreach (var p in Prefixes)
            if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// A priority rule with everything device software must not get taken out: a
    /// below-normal priority becomes Normal and any memory cap is dropped. Raising a
    /// device app's priority is allowed. Other rules come back unchanged.
    /// </summary>
    public static PriorityRule Sanitize(PriorityRule rule)
    {
        if (!IsProtected(rule.ExePath)) return rule;
        if (rule.Priority != PriorityLevel.BelowNormal && rule.MemoryLimitMb is null) return rule;
        return rule with
        {
            Priority = rule.Priority == PriorityLevel.BelowNormal ? PriorityLevel.Normal : rule.Priority,
            MemoryLimitMb = null,
            LimitAutoAssigned = false,
        };
    }

    /// <summary>"C:\x\lghub_agent.exe" → "lghub_agent". Never throws.</summary>
    public static string Normalize(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return "";
        var s = processName.Trim();
        var slash = s.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0) s = s[(slash + 1)..];
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        return s.Trim();
    }
}
