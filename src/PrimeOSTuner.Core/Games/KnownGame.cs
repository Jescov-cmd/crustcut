namespace PrimeOSTuner.Core.Games;

public enum KnownGameSource
{
    Steam,
    StaticCatalog,
    UserAdded
}

public sealed record KnownGame(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ExecutableNames,
    string? SteamAppId,
    string? InstallPath,
    KnownGameSource Source);
