using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Settings;

public sealed class AppSettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettingsStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "app-settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = ResilientJsonFile.ReadText(_path);
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (WriteGate)
            ResilientJsonFile.WriteText(_path, JsonSerializer.Serialize(settings, JsonOpts));
    }

    // One gate for every store instance in the process: fans, game-only apps, the overlay
    // and the Settings page each build their own store, and load-change-save sequences
    // from different threads used to overwrite each other's changes.
    private static readonly object WriteGate = new();

    /// <summary>
    /// Reads the file fresh, applies <paramref name="change"/>, writes it back — atomically
    /// with respect to other updates in this process. If the file exists but can't be read
    /// (antivirus lock, half-written), NOTHING is written: <see cref="Load"/> would have
    /// returned defaults, and saving those wiped the user's real settings.
    /// </summary>
    /// <returns>The settings as saved, or null when the read failed and nothing changed.</returns>
    public AppSettings? Update(Action<AppSettings> change)
    {
        lock (WriteGate)
        {
            AppSettings current;
            try
            {
                var json = File.Exists(_path) ? ResilientJsonFile.ReadText(_path) : null;
                current = string.IsNullOrWhiteSpace(json)
                    ? new AppSettings()
                    : JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return null;
            }
            change(current);
            ResilientJsonFile.WriteText(_path, JsonSerializer.Serialize(current, JsonOpts));
            return current;
        }
    }
}
