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

    public static bool IsMinecraft(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return false;
        foreach (var m in Markers)
            if (commandLine.Contains(m, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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
