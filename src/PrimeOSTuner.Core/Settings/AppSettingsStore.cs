using System.Text.Json;

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
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
