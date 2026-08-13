using System.Diagnostics;
using System.IO.Compression;
using PrimeOSTuner.Core.Updates;

namespace Crustcut.App.Services;

/// <summary>
/// Downloads a release zip and swaps it in. A running executable cannot overwrite itself,
/// so the actual file replacement is done by a small script that waits for this process to
/// exit, copies the new files over the install folder, and starts the new build. That is
/// the standard portable-app update dance, and it is deliberately verbose in the log
/// because a half-finished update is the worst possible outcome for a tool like this.
/// </summary>
public sealed class UpdateInstaller
{
    private readonly HttpClient _http;

    public UpdateInstaller(HttpClient http) => _http = http;

    /// <summary>
    /// Fetches and stages the update, then hands off to the swap script. On success this
    /// method does not return normally — the caller shuts the app down.
    /// Returns an error message when the update could not be staged.
    /// </summary>
    public async Task<string?> DownloadAndApplyAsync(
        AvailableUpdate update, Action shutdown, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var staging = Path.Combine(Path.GetTempPath(), $"crustcut-update-{Guid.NewGuid():N}");
        try
        {
            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var exePath = Environment.ProcessPath;
            if (exePath is null) return "Cannot locate the running program.";

            Directory.CreateDirectory(staging);
            var zipPath = Path.Combine(staging, "update.zip");

            progress?.Report("Downloading…");
            EngineLog.Log($"update: downloading {update.Version} from {update.DownloadUrl}");
            using (var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!response.IsSuccessStatusCode)
                    return $"Download failed ({(int)response.StatusCode}).";
                await using var file = File.Create(zipPath);
                await response.Content.CopyToAsync(file, ct);
            }

            progress?.Report("Unpacking…");
            var newFiles = Path.Combine(staging, "new");
            ZipFile.ExtractToDirectory(zipPath, newFiles);

            // Some zips wrap everything in a single folder; use it if so.
            var payload = newFiles;
            var entries = Directory.GetFileSystemEntries(newFiles);
            if (entries.Length == 1 && Directory.Exists(entries[0])) payload = entries[0];

            var newExe = Path.Combine(payload, Path.GetFileName(exePath));
            if (!File.Exists(newExe))
                return "The downloaded package didn't contain the program — update cancelled.";

            progress?.Report("Restarting…");
            LaunchSwapScript(staging, payload, installDir, exePath);
            EngineLog.Log($"update: staged {update.Version}, restarting to apply");
            shutdown();
            return null;
        }
        catch (Exception ex)
        {
            EngineLog.Log($"update: failed: {ex.Message}");
            try { Directory.Delete(staging, recursive: true); } catch { }
            return $"Update failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes and starts the swap script. It waits for our PID to disappear before copying
    /// anything — copying over a running executable is exactly how installs get corrupted.
    /// </summary>
    private static void LaunchSwapScript(string staging, string payload, string installDir, string exePath)
    {
        var script = Path.Combine(staging, "apply-update.cmd");
        var pid = Environment.ProcessId;

        File.WriteAllText(script, $"""
@echo off
rem Wait for Crustcut (PID {pid}) to exit before touching its files.
:wait
tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait
)
rem /IS overwrites same-size files too; retries cover a straggling file lock.
robocopy "{payload}" "{installDir}" /E /IS /R:3 /W:1 >nul
start "" "{exePath}"
rem Clean up the staging folder, script included.
timeout /t 2 /nobreak >nul
rmdir /s /q "{staging}"
""");

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        });
    }
}
