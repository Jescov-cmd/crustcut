using System.Text.Json;

namespace PrimeOSTuner.Core.Updates;

/// <summary>A newer release than the one running.</summary>
/// <param name="Version">Release version, tag stripped of its leading "v".</param>
/// <param name="DownloadUrl">Direct link to this platform's zip asset.</param>
/// <param name="Notes">Release notes body, for the "what's new" panel.</param>
/// <param name="PageUrl">Human-facing release page, for the manual download fallback.</param>
public sealed record AvailableUpdate(string Version, string DownloadUrl, string Notes, string PageUrl);

/// <summary>
/// Asks GitHub whether a newer release exists. Deliberately quiet: any failure (offline,
/// rate-limited, malformed) returns null, because an update check must never interrupt a
/// tool whose whole job is staying out of the way.
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/Jescov-cmd/crustcut/releases/latest";

    private readonly HttpClient _http;
    private readonly string _assetKeyword;

    public UpdateChecker(HttpClient http, string? assetKeyword = null)
    {
        _http = http;
        // Each platform ships its own zip; pick the one this build can actually run.
        _assetKeyword = assetKeyword ?? (OperatingSystem.IsMacOS() ? "mac" : "win");
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent", "Crustcut");
    }

    /// <summary>Newer release, or null when up to date or the check couldn't complete.</summary>
    public async Task<AvailableUpdate?> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(LatestReleaseApi, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (!TryParseTag(tag, out var latest) || latest <= current) return null;

            var url = FindAssetUrl(root);
            if (url is null) return null;

            return new AvailableUpdate(
                Version: latest.ToString(3),
                DownloadUrl: url,
                Notes: root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                PageUrl: root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "");
        }
        catch
        {
            return null;   // offline, throttled, or GitHub changed shape — never surface it
        }
    }

    /// <summary>"v0.9.4" / "0.9.4" → 0.9.4. Anything else is not a release we understand.</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var trimmed = tag.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out version!);
    }

    private string? FindAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains(_assetKeyword, StringComparison.OrdinalIgnoreCase)) continue;
            if (asset.TryGetProperty("browser_download_url", out var u)) return u.GetString();
        }
        return null;
    }
}
