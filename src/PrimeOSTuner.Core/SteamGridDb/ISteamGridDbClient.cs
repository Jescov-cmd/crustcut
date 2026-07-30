namespace PrimeOSTuner.Win.SteamGridDb;

public interface ISteamGridDbClient
{
    bool HasApiKey { get; }
    Task<CoverArt> GetCoverByAppIdAsync(string steamAppId, CancellationToken ct = default);
    Task<IReadOnlyList<SgdbGameRef>> SearchAsync(string query, CancellationToken ct = default);
    Task<CoverArt> GetCoverByGameIdAsync(long sgdbGameId, string fallbackName, CancellationToken ct = default);
}
