using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Sentinel;

public sealed class SteamSpecFetcher : ISpecFetcher
{
    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly Dictionary<string, SteamPcRequirements> _cache = new();
    private readonly object _gate = new();

    public SteamSpecFetcher(HttpClient http, string? cachePath = null)
    {
        _http = http;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://store.steampowered.com");
        _cachePath = cachePath ?? DefaultCachePath();
        LoadCacheFromDisk();
    }

    public static string DefaultCachePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "sentinel-specs.json");

    public async Task<SteamPcRequirements?> FetchAsync(string steamAppId, CancellationToken ct = default)
    {
        lock (_gate) if (_cache.TryGetValue(steamAppId, out var hit)) return hit;

        try
        {
            var resp = await _http.GetFromJsonAsync<Dictionary<string, AppDetailsEnvelope>>(
                $"/api/appdetails?appids={steamAppId}&filters=basic", ct);
            if (resp is null || !resp.TryGetValue(steamAppId, out var env) || !env.Success || env.Data is null)
                return null;

            var minHtml = env.Data.PcRequirements?.Minimum ?? "";
            var recHtml = env.Data.PcRequirements?.Recommended ?? "";

            var min = SteamSpecParser.ParseMinimum(minHtml);
            var rec = SteamSpecParser.ParseRecommended(recHtml);
            var merged = SteamSpecParser.Merge(min, rec);

            lock (_gate) _cache[steamAppId] = merged;
            SaveCacheToDisk();
            return merged;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private void LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            var json = File.ReadAllText(_cachePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, SteamPcRequirements>>(json);
            if (loaded is null) return;
            lock (_gate) foreach (var kv in loaded) _cache[kv.Key] = kv.Value;
        }
        catch { /* corrupt cache → just start fresh */ }
    }

    private void SaveCacheToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            Dictionary<string, SteamPcRequirements> snapshot;
            lock (_gate) snapshot = new Dictionary<string, SteamPcRequirements>(_cache);
            var tmp = _cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            File.Move(tmp, _cachePath, overwrite: true);
        }
        catch { /* cache writes are best-effort */ }
    }

    private sealed class AppDetailsEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("data")] public AppDetailsData? Data { get; set; }
    }

    private sealed class AppDetailsData
    {
        [JsonPropertyName("pc_requirements")] public PcRequirementsBlob? PcRequirements { get; set; }
    }

    private sealed class PcRequirementsBlob
    {
        [JsonPropertyName("minimum")] public string? Minimum { get; set; }
        [JsonPropertyName("recommended")] public string? Recommended { get; set; }
    }
}
