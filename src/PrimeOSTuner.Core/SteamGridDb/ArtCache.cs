namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class ArtCache
{
    private readonly string _cacheDir;
    private readonly HttpClient _http;

    public ArtCache(string cacheDir, HttpClient http)
    {
        _cacheDir = cacheDir;
        _http = http;
        Directory.CreateDirectory(_cacheDir);
    }

    public static string DefaultDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "art-cache");

    public async Task<string?> GetOrDownloadAsync(long gameId, string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var path = Path.Combine(_cacheDir, $"{gameId}.jpg");
        if (File.Exists(path)) return path;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
