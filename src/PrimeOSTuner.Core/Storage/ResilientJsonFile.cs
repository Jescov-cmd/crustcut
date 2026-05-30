using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace PrimeOSTuner.Core.Storage;

/// <summary>
/// Reads/writes small JSON files robustly. Two problems this solves, both of which were
/// crashing the app with "the process cannot access the file ... because it is being used
/// by another process":
///   1. Concurrent access from our OWN code (UI saving while a background task loads, or
///      rapid successive saves). Serialized here with a per-path lock.
///   2. Transient locks from OTHER processes (antivirus, Search indexer, backup). Handled
///      by opening with FileShare and retrying briefly.
/// Per-path locks (keyed by full path) mean different files never contend with each other.
/// </summary>
public static class ResilientJsonFile
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxRetries = 8;
    private const int RetryDelayMs = 75;

    private static SemaphoreSlim GateFor(string path) =>
        Locks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

    // ---- async ----------------------------------------------------------------

    public static async Task<string?> ReadTextAsync(string path)
    {
        if (!File.Exists(path)) return null;
        var gate = GateFor(path);
        // ConfigureAwait(false) throughout: this is library I/O and is sometimes called
        // sync-over-async at startup — never capture the UI context, or that can deadlock.
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    return await sr.ReadToEndAsync().ConfigureAwait(false);
                }
                catch (IOException) when (attempt < MaxRetries) { await Task.Delay(RetryDelayMs).ConfigureAwait(false); }
            }
        }
        finally { gate.Release(); }
    }

    public static async Task WriteTextAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var gate = GateFor(path);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs);
                    await sw.WriteAsync(contents).ConfigureAwait(false);
                    return;
                }
                catch (IOException) when (attempt < MaxRetries) { await Task.Delay(RetryDelayMs).ConfigureAwait(false); }
            }
        }
        finally { gate.Release(); }
    }

    public static async Task DeleteAsync(string path)
    {
        var gate = GateFor(path);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try { if (File.Exists(path)) File.Delete(path); return; }
                catch (IOException) when (attempt < MaxRetries) { await Task.Delay(RetryDelayMs).ConfigureAwait(false); }
            }
        }
        finally { gate.Release(); }
    }

    // ---- sync (for callers that aren't async, e.g. AppSettingsStore) ----------

    public static string? ReadText(string path)
    {
        if (!File.Exists(path)) return null;
        var gate = GateFor(path);
        gate.Wait();
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    return sr.ReadToEnd();
                }
                catch (IOException) when (attempt < MaxRetries) { Thread.Sleep(RetryDelayMs); }
            }
        }
        finally { gate.Release(); }
    }

    public static void WriteText(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var gate = GateFor(path);
        gate.Wait();
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs);
                    sw.Write(contents);
                    return;
                }
                catch (IOException) when (attempt < MaxRetries) { Thread.Sleep(RetryDelayMs); }
            }
        }
        finally { gate.Release(); }
    }
}
