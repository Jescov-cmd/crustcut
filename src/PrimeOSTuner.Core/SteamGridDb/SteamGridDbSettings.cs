using System.Text.Json;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SteamGridDbSettings
{
    public string? SteamGridDbApiKey { get; set; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "settings.json");

    public static SteamGridDbSettings Load(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return new SteamGridDbSettings();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SteamGridDbSettings>(json) ?? new SteamGridDbSettings();
        }
        catch
        {
            return new SteamGridDbSettings();
        }
    }
}
