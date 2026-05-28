using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Win.Steam;

/// <summary>
/// Hits Steam Store's public search endpoint:
/// <c>https://store.steampowered.com/api/storesearch/?term=&lt;name&gt;&amp;l=en&amp;cc=US</c>
/// Returns up to ~10 candidate matches; we pick the first <c>type==app</c> entry,
/// which Steam already ranks by relevance.
/// </summary>
public sealed class SteamAppLookup : ISteamAppLookup
{
    private readonly HttpClient _http;

    public SteamAppLookup(HttpClient http)
    {
        _http = http;
        // The HTTPClient may already have a BaseAddress set by the DI factory; only set it if not.
        _http.BaseAddress ??= new Uri("https://store.steampowered.com");
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public async Task<SteamAppMatch?> ResolveAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        try
        {
            var url = $"/api/storesearch/?term={Uri.EscapeDataString(title)}&l=en&cc=US";
            var resp = await _http.GetFromJsonAsync<StoreSearchResponse>(url, ct);
            if (resp?.Items is null) return null;

            // Pick the first result Steam classifies as an app (filters out DLC, soundtracks, demos).
            var hit = resp.Items.FirstOrDefault(i => string.Equals(i.Type, "app", StringComparison.OrdinalIgnoreCase));
            if (hit is null || hit.Id <= 0 || string.IsNullOrWhiteSpace(hit.Name)) return null;

            return new SteamAppMatch(hit.Id.ToString(), hit.Name);
        }
        catch
        {
            return null;
        }
    }

    private sealed class StoreSearchResponse
    {
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("items")] public List<StoreSearchItem>? Items { get; set; }
    }

    private sealed class StoreSearchItem
    {
        [JsonPropertyName("id")]   public int    Id   { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
