using System.IO;

namespace PrimeOSTuner.Core.Memory;

public static class CustomModeMigration
{
    /// <summary>
    /// On first v0.4 launch, if the old custom-mode.json exists, back it up to *.bak.v0.3
    /// so the user's prior data is preserved (we don't import it into Memory Priority).
    /// Idempotent: subsequent calls are no-ops.
    /// </summary>
    public static void RunIfNeeded()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner");
        var oldFile = Path.Combine(dir, "custom-mode.json");
        var backup = Path.Combine(dir, "custom-mode.json.bak.v0.3");

        if (!File.Exists(oldFile)) return;
        if (File.Exists(backup)) return;

        try
        {
            File.Move(oldFile, backup);
        }
        catch
        {
            // Best-effort. If migration fails, the file stays in place; nothing is lost.
        }
    }
}
