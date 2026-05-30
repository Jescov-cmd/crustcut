namespace PrimeOSTuner.Win.Launchers;

/// <summary>Which storefront/launcher a game was installed through.</summary>
public enum GameLauncher
{
    Epic,
    Ubisoft,
    Ea,
    Gog,
}

/// <summary>A game discovered via a non-Steam launcher.</summary>
public sealed record ExternalGame(
    string Id,
    string Name,
    string? PrimaryExecutablePath,
    GameLauncher Launcher);

/// <summary>
/// Pluggable scanner for one launcher (Epic, Ubisoft, EA, GOG, …). Implementations are
/// registered in DI and aggregated by GameRegistry, so adding a new storefront is just a
/// new class — no changes to the registry. Scans must never throw.
/// </summary>
public interface IExternalGameScanner
{
    GameLauncher Launcher { get; }
    IReadOnlyList<ExternalGame> Scan();
}
