using System.ServiceProcess;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Clears <c>C:\Windows\SoftwareDistribution\Download</c> — Windows Update's pending-package
/// cache. Often grows to several GB and frequently fixes "Windows Update stuck" symptoms.
/// Stops the wuauserv service first so files aren't held open, then restarts it.
/// </summary>
public sealed class WindowsUpdateCacheTweak : ITweak
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SoftwareDistribution", "Download");

    private const string ServiceName = "wuauserv";

    public string Id => "core.windows-update-cache";
    public string DisplayName => "Clear Windows Update cache";
    public string Description => "Frees disk space and unsticks Windows Update.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(CachePath)) return Task.FromResult(TweakState.Applied);
            var hasFiles = Directory.EnumerateFileSystemEntries(CachePath).Any();
            return Task.FromResult(hasFiles ? TweakState.NotApplied : TweakState.Applied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(CachePath))
                return TweakResult.Success(message: "Cache folder not present — nothing to clear.");

            await StopServiceAsync(ServiceName, ct);

            long freed = 0;
            int errors = 0;
            foreach (var path in EnumerateSafe(CachePath))
            {
                try
                {
                    var len = SafeLength(path);
                    File.Delete(path);
                    freed += len;
                }
                catch { errors++; }
            }
            // Best-effort prune of empty subdirectories
            foreach (var dir in Directory.EnumerateDirectories(CachePath, "*", SearchOption.AllDirectories)
                                          .OrderByDescending(d => d.Length))
            {
                try { Directory.Delete(dir, recursive: false); } catch { }
            }

            await StartServiceAsync(ServiceName, ct);

            var mb = freed / 1024.0 / 1024.0;
            var msg = errors > 0
                ? $"Cleared {mb:F1} MB ({errors} files in use)."
                : $"Cleared {mb:F1} MB.";
            return TweakResult.Success(message: msg);
        }
        catch (Exception ex)
        {
            // Best effort: try to leave the service running even if we crashed.
            try { await StartServiceAsync(ServiceName, ct); } catch { }
            return TweakResult.Failure(ex.Message);
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Windows Update cache cannot be reverted."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(CachePath))
            return Task.FromResult("Cache folder not present.");

        long total = 0;
        foreach (var f in EnumerateSafe(CachePath)) total += SafeLength(f);
        return Task.FromResult($"Will free approximately {total / 1024.0 / 1024.0:F1} MB.");
    }

    private static IEnumerable<string> EnumerateSafe(string dir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        IEnumerable<string> source;
        try { source = Directory.EnumerateFiles(dir, "*", options); }
        catch { yield break; }

        using var iter = source.GetEnumerator();
        while (true)
        {
            string current;
            try
            {
                if (!iter.MoveNext()) yield break;
                current = iter.Current;
            }
            catch { continue; }
            yield return current;
        }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static async Task StopServiceAsync(string name, CancellationToken ct)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending) return;
            sc.Stop();
            // Use CancellationToken-aware polling because WaitForStatus is sync-only.
            for (int i = 0; i < 30 && sc.Status != ServiceControllerStatus.Stopped; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(500, ct);
                sc.Refresh();
            }
        }
        catch { /* if we can't stop, we'll just try delete with files possibly held open */ }
    }

    private static async Task StartServiceAsync(string name, CancellationToken ct)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending) return;
            sc.Start();
            for (int i = 0; i < 30 && sc.Status != ServiceControllerStatus.Running; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(500, ct);
                sc.Refresh();
            }
        }
        catch { }
    }
}
