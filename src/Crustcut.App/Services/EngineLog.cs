using System.Text;

namespace Crustcut.App.Services;

/// <summary>
/// Tiny append-only diagnostic log for the background engine. Exists because every engine
/// failure used to vanish into an empty catch — "the timer isn't working" was undebuggable
/// for the user AND for us. Never throws; size-capped so it can't grow unbounded.
/// </summary>
public static class EngineLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 256 * 1024;

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "engine.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                var path = DefaultPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    // Keep the newest half so the log survives forever without ballooning.
                    var lines = File.ReadAllLines(path);
                    File.WriteAllLines(path, lines.Skip(lines.Length / 2));
                }
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch { /* diagnostics must never hurt the app */ }
    }
}
