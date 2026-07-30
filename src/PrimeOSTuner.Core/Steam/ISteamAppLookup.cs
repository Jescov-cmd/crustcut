namespace PrimeOSTuner.Win.Steam;

/// <summary>
/// Resolves a free-text game name to a Steam AppID using the public Steam Store
/// search endpoint. Used by the Add-Game flow to auto-link manually-added games
/// (e.g. R6 Siege bought on Ubisoft Connect) to their Steam listing so Sentinel
/// can pull the recommended-spec data.
/// </summary>
public interface ISteamAppLookup
{
    /// <summary>
    /// Returns the best-match Steam AppID for the given title, or null if nothing
    /// confidently matched. Network failures return null silently.
    /// </summary>
    Task<SteamAppMatch?> ResolveAsync(string title, CancellationToken ct = default);
}

public sealed record SteamAppMatch(string AppId, string OfficialName);
