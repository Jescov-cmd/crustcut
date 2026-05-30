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
        ResilientJsonFile.WriteText(_path, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
