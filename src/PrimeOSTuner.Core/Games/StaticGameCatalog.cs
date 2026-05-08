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
        new KnownGame(
            Id: "static.minecraft",
            DisplayName: "Minecraft",
            ExecutableNames: new[] { "Minecraft.Windows.exe", "javaw.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
    };
}
