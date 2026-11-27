namespace PrimeOSTuner.Core.Games;

/// <summary>
/// Recognises Minecraft inside a Java process. Modded Minecraft never runs as
/// "minecraft.exe" — Modrinth, CurseForge, Prism, MultiMC and the vanilla launcher all
/// spawn a plain javaw.exe, which exe-name game detection is blind to. The result on a
/// real machine: the user played for hours while Crustcut treated the machine as idle —
/// no game protection, and the memory engine cleaning mid-game (reported as unstable
/// FPS). The command line, however, is unmistakable.
/// </summary>
public static class JavaGameHeuristics
{
    // Any one of these in a java/javaw command line means Minecraft, across vanilla,
    // Fabric, Forge, NeoForge, Quilt and the popular third-party launchers/clients.
    private static readonly string[] Markers =
    {
        "net.minecraft",            // vanilla main class + libraries
        "net.fabricmc",             // Fabric loader
        "minecraftforge",           // Forge
        "neoforged",                // NeoForge
        "cpw.mods.bootstraplauncher", // modern Forge bootstrap
        "org.quiltmc",              // Quilt
        ".minecraft",               // the universal game directory
        "lunarclient",              // Lunar
        "modrinthapp",              // Modrinth's bundled runtimes/profile paths
        "curseforge",               // CurseForge instance paths
    };

    // Build tools and mod-development runs carry the same library names on their command
    // line (a NeoForge/Fabric mod's Gradle daemon has net.minecraft and neoforged all over
    // its classpath) but aren't someone playing. Counted as Minecraft, every mod build
    // became a 50-second "game": a session with 0 frames, background apps paused and —
    // with Game mode on — the power plan switched, all for a compiler.
    private static readonly string[] NotPlaying =
    {
        "org.gradle",               // Gradle wrapper, daemon and workers
        "gradlewrapper",
        "gradle-wrapper",
        "gradledaemon",
        "gradleworkermain",
        "kotlin.daemon",            // Kotlin compile daemon
        "kotlincompiledaemon",
        "devlaunch",                // NeoForge/Fabric dev runs (net.neoforged.devlaunch, devlaunchinjector)
        "fabric.development=true",
        "gametest",                 // automated game-test servers
        "--nogui",                  // dedicated servers
        "net.minecraft.server.main",
    };

    public static bool IsMinecraft(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return false;
        foreach (var n in NotPlaying)
            if (commandLine.Contains(n, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var m in Markers)
            if (commandLine.Contains(m, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// The Java heap ceiling from a command line's -Xmx flag ("-Xmx8G", "-Xmx6144m",
    /// "-Xmx4096M"), in bytes; null when absent or unreadable. Launchers set this from the
    /// "memory allocated" slider, and it is the single biggest RAM decision in the game.
    /// </summary>
    public static long? MaxHeapBytes(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(commandLine, @"-Xmx(\d+)([kKmMgG]?)(?=\s|""|$)");
        if (!m.Success || !long.TryParse(m.Groups[1].Value, out var n)) return null;
        return m.Groups[2].Value.ToLowerInvariant() switch
        {
            "g" => n << 30,
            "m" => n << 20,
            "k" => n << 10,
            _ => n,
        };
    }

    /// <summary>The synthetic game every detected Java Minecraft maps to — one id, so
    /// profiles/rules assigned to it survive launcher switches and version updates.</summary>
    public static readonly KnownGame MinecraftJava = new(
        Id: "java.minecraft",
        DisplayName: "Minecraft (Java)",
        ExecutableNames: new[] { "javaw.exe", "java.exe" },
        SteamAppId: null,
        InstallPath: null,
        Source: KnownGameSource.StaticCatalog);
}
