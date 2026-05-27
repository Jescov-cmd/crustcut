namespace PrimeOSTuner.Core.Sentinel;

public interface ISpecFetcher
{
    /// <summary>
    /// Returns the parsed Steam spec for a Steam app id. Returns null on any
    /// failure — network, parse, missing data. Implementations should cache
    /// results so a fresh app launch doesn't re-hit Steam for known games.
    /// </summary>
    Task<SteamPcRequirements?> FetchAsync(string steamAppId, CancellationToken ct = default);
}
