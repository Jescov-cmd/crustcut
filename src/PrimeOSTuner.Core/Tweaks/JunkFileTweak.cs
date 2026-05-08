using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class JunkFileTweak : ITweak
{
    private readonly string[] _targetDirs;

    public string Id => "core.junk-files";
    public string DisplayName => "Clear junk files";
    public string Description => "Removes temp files, browser caches, and Windows update cache.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public JunkFileTweak() : this(DefaultTargets()) { }
    public JunkFileTweak(string[] targetDirs) { _targetDirs = targetDirs; }

    private static string[] DefaultTargets() =>
        new[]
        {
            Environment.ExpandEnvironmentVariables("%TEMP%"),
            Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Temp"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\Windows\INetCache"),
        };

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        long junk = 0;
        foreach (var dir in _targetDirs)
            if (Directory.Exists(dir))
                foreach (var f in EnumerateSafe(dir))
                    junk += SafeLength(f);

        return Task.FromResult(junk > 0 ? TweakState.NotApplied : TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        var totalDirs = _targetDirs.Length;
        for (int i = 0; i < totalDirs; i++)
        {
            ct.ThrowIfCancellationRequested();
            var dir = _targetDirs[i];
            if (!Directory.Exists(dir)) continue;

            foreach (var f in EnumerateSafe(dir))
            {
                try
                {
                    var len = SafeLength(f);
                    File.Delete(f);
                    freed += len;
                }
                catch { /* in-use file - skip */ }
            }
            progress?.Report((int)((i + 1) / (double)totalDirs * 100));
        }

        var undo = JsonSerializer.Serialize(new { freed });
        return Task.FromResult(TweakResult.Success(undo));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Junk file deletion cannot be reverted."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        long total = 0;
        foreach (var dir in _targetDirs)
            if (Directory.Exists(dir))
                foreach (var f in EnumerateSafe(dir))
                    total += SafeLength(f);
        return Task.FromResult($"Will delete approximately {total / 1024.0 / 1024.0:F1} MB across {_targetDirs.Length} folders.");
    }

    private static IEnumerable<string> EnumerateSafe(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }
}
