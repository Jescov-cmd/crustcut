using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SteamGridDbClient : ISteamGridDbClient
{
    private const string BaseUri = "https://www.steamgriddb.com";
    private readonly HttpClient _http;
    private readonly SteamGridDbSettings _settings;

    public SteamGridDbClient(HttpClient http, SteamGridDbSettings settings)
    {
        _http = http;
        _settings = settings;
        if (_http.BaseAddress is null) _http.BaseAddress = new Uri(BaseUri);
        if (HasApiKey)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.SteamGridDbApiKey);
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_settings.SteamGridDbApiKey);

    public async Task<CoverArt> GetCoverByAppIdAsync(string steamAppId, CancellationToken ct = default)
    {
        if (!HasApiKey) return new CoverArt(null, "", null, null);

        try
        {
            var lookup = await _http.GetFromJsonAsync<SgdbResponse<SgdbGameRef>>(
                $"/api/v2/games/steam/{steamAppId}", ct);
            if (lookup?.Success != true || lookup.Data is null)
                return new CoverArt(null, "", null, null);

            return await GetCoverByGameIdAsync(lookup.Data.Id, lookup.Data.Name, ct);
        }
        catch
        {
            return new CoverArt(null, "", null, null);
        }
    }

    public async Task<CoverArt> GetCoverByGameIdAsync(long sgdbGameId, string fallbackName, CancellationToken ct = default)
    {
        if (!HasApiKey) return new CoverArt(sgdbGameId, fallbackName, null, null);
        try
        {
            var grids = await _http.GetFromJsonAsync<SgdbResponse<List<SgdbGrid>>>(
                $"/api/v2/grids/game/{sgdbGameId}?dimensions=600x900", ct);
            var first = grids?.Data?.FirstOrDefault();
            return new CoverArt(sgdbGameId, fallbackName, first?.Url, first?.Thumb);
        }
        catch
        {
            return new CoverArt(sgdbGameId, fallbackName, null, null);
        }
    }

    public async Task<IReadOnlyList<SgdbGameRef>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!HasApiKey) return Array.Empty<SgdbGameRef>();
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var resp = await _http.GetFromJsonAsync<SgdbResponse<List<SgdbGameRef>>>(
                $"/api/v2/search/autocomplete/{encoded}", ct);
            return resp?.Data ?? new List<SgdbGameRef>();
        }
        catch
        {
            return Array.Empty<SgdbGameRef>();
        }
    }
}
