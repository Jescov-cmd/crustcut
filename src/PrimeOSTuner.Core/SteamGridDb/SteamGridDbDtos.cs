using System.Text.Json.Serialization;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SgdbResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")]    public T? Data { get; set; }
    [JsonPropertyName("errors")]  public List<string>? Errors { get; set; }
}

public sealed class SgdbGameRef
{
    [JsonPropertyName("id")]   public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class SgdbGrid
{
    [JsonPropertyName("id")]     public long Id { get; set; }
    [JsonPropertyName("url")]    public string Url { get; set; } = "";
    [JsonPropertyName("thumb")]  public string Thumb { get; set; } = "";
    [JsonPropertyName("width")]  public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

/// <summary>What the rest of the app sees: a resolved cover-art URL plus minimal metadata. Null URL = no art available.</summary>
public sealed record CoverArt(long? GameId, string GameName, string? Url, string? Thumb);
