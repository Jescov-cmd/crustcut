namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Deletes per-vendor GPU shader caches. Games rebuild them the next time you
/// launch, so first-run after cleanup may show extra compile stutter — but
/// stale/corrupt cache after driver updates causes the same stutter every run,
/// which this fixes.
///
/// Touches: %LOCALAPPDATA%\NVIDIA\{GLCache,DXCache},
/// %LOCALAPPDATA%\AMD\{DxCache,GLCache}, and %LOCALAPPDATA%\D3DSCache.
/// </summary>
public sealed class ShaderCacheCleanupTweak : ITweak
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string[] CachePaths =
    {
        Path.Combine(LocalAppData, "NVIDIA", "GLCache"),
        Path.Combine(LocalAppData, "NVIDIA", "DXCache"),
        Path.Combine(LocalAppData, "AMD", "DxCache"),
        Path.Combine(LocalAppData, "AMD", "GLCache"),
        Path.Combine(LocalAppData, "D3DSCache"),
    };

    public string Id => "core.shader-cache-cleanup";
    public string DisplayName => "Clear GPU shader cache";
    public string Description => "Wipes NVIDIA / AMD / DirectX shader caches. Fixes stutter after driver updates; games rebuild the cache on next launch.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var hasAny = CachePaths.Any(p => Directory.Exists(p) && Directory.EnumerateFileSystemEntries(p).Any());
            return Task.FromResult(hasAny ? TweakState.NotApplied : TweakState.Applied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        int errors = 0;
        int touched = 0;

        foreach (var dir in CachePaths)
        {
            if (!Directory.Exists(dir)) continue;
            touched++;

            foreach (var path in EnumerateSafe(dir))
            {
                try
                {
                    var len = SafeLength(path);
                    File.Delete(path);
                    freed += len;
                }
                catch { errors++; }
            }
        }

        if (touched == 0)
            return Task.FromResult(TweakResult.Success(message: "No shader caches present — nothing to clear."));

        var mb = freed / 1024.0 / 1024.0;
        var msg = errors > 0
            ? $"Cleared {mb:F1} MB across {touched} cache(s) ({errors} files in use)."
            : $"Cleared {mb:F1} MB across {touched} cache(s).";
        return Task.FromResult(TweakResult.Success(message: msg));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Shader cache cleanup cannot be reverted — caches rebuild automatically."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        long total = 0;
        int folders = 0;
        foreach (var dir in CachePaths)
        {
            if (!Directory.Exists(dir)) continue;
            folders++;
            foreach (var f in EnumerateSafe(dir)) total += SafeLength(f);
        }
        if (folders == 0) return Task.FromResult("No shader cache folders present.");
        return Task.FromResult($"Will free approximately {total / 1024.0 / 1024.0:F1} MB across {folders} cache(s).");
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
}
