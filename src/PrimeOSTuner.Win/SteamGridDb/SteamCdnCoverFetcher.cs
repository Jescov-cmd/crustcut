namespace PrimeOSTuner.Win.SteamGridDb;

/// <summary>
/// Fetches game cover art directly from Steam's public CDN — no API key,
/// no rate limit, just the same URL Steam itself loads for the library
/// page. The <c>library_600x900.jpg</c> asset matches our 220×320 game-
/// card aspect ratio.
///
/// Some older games never had a library_600x900 published; for those we
/// return null and the caller can fall back to SteamGridDB (which needs
/// an API key) or just show a placeholder.
/// </summary>
public sealed class SteamCdnCoverFetcher
{
    private const string UrlTemplate =
        "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/library_600x900.jpg";

    private readonly ArtCache _cache;

    public SteamCdnCoverFetcher(ArtCache cache) { _cache = cache; }

    /// <summary>
    /// Returns the local cache path of the downloaded cover image, or null
    /// if the Steam app id is unparseable or the CDN returned no asset.
    /// </summary>
    public Task<string?> FetchCoverAsync(string steamAppId, CancellationToken ct = default)
    {
        if (!long.TryParse(steamAppId, out var appId)) return Task.FromResult<string?>(null);
        var url = string.Format(UrlTemplate, appId);
        return _cache.GetOrDownloadAsync(appId, url, ct);
    }
}
