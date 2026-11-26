namespace PrimeOSTuner.Core.Games;

public static class StaticGameCatalog
{
    public static readonly IReadOnlyList<KnownGame> All = new[]
    {
        new KnownGame(
            Id: "static.valorant",
            DisplayName: "VALORANT",
            ExecutableNames: new[] { "VALORANT-Win64-Shipping.exe", "VALORANT.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.league-of-legends",
            DisplayName: "League of Legends",
            ExecutableNames: new[] { "League of Legends.exe", "LeagueClient.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.fortnite",
            DisplayName: "Fortnite",
            ExecutableNames: new[] { "FortniteClient-Win64-Shipping.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.apex-legends",
            DisplayName: "Apex Legends",
            ExecutableNames: new[] { "r5apex.exe" },
            SteamAppId: "1172470",
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.overwatch",
            DisplayName: "Overwatch 2",
            ExecutableNames: new[] { "Overwatch.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        // Bedrock only. Java Edition is NOT matched by name here: "javaw.exe" is every Java
        // program — Gradle builds, mod dev servers, IDE tooling — so listing it made each
        // build count as a game session. JavaGameDetector reads command lines instead and
        // reports real Java Minecraft as "Minecraft (Java)".
        new KnownGame(
            Id: "static.minecraft",
            DisplayName: "Minecraft",
            ExecutableNames: new[] { "Minecraft.Windows.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        // RobloxPlayerBeta.exe is the game whether it's started by Roblox's own launcher,
        // Bloxstrap or Fishstrap (they launch the same player). Windows10Universal.exe is the
        // Microsoft Store edition. Roblox Studio is left out on purpose: it's a dev tool.
        new KnownGame(
            Id: "static.roblox",
            DisplayName: "Roblox",
            ExecutableNames: new[] { "RobloxPlayerBeta.exe", "Windows10Universal.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
    };
}
