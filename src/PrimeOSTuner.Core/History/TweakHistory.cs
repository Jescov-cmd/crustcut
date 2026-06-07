using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PrimeOSTuner.Core.History;

public sealed class TweakHistory
{
    private readonly string _filePath;
    private readonly TimeSpan? _retention;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    /// <param name="retention">If set, entries older than this are dropped (and the file
    /// rewritten) whenever history is loaded — e.g. 24h means history self-expires after a day.</param>
    public TweakHistory(string filePath, TimeSpan? retention = null)
    {
        _filePath = filePath;
        _retention = retention;
    }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "history.json");

    public async Task AppendAsync(HistoryEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            entries.Add(entry);
            await SaveAsync(entries);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync()
    {
        await _lock.WaitAsync();
        try { return await LoadInternalAsync(); }
        finally { _lock.Release(); }
    }

    /// <summary>Removes all history entries.</summary>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try { await SaveAsync(new List<HistoryEntry>()); }
        finally { _lock.Release(); }
    }

    public async Task MarkRevertedAsync(Guid entryId)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == entryId)
                {
                    entries[i] = entries[i] with { Reverted = true };
                }
            }
            await SaveAsync(entries);
        }
        finally { _lock.Release(); }
    }

    private async Task<List<HistoryEntry>> LoadInternalAsync()
    {
        if (!File.Exists(_filePath)) return new List<HistoryEntry>();
        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<HistoryEntry>();
        var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts) ?? new List<HistoryEntry>();

        // Self-expiry: drop entries older than the retention window and persist the trim so the
        // file doesn't grow unbounded.
        if (_retention is { } retention)
        {
            var cutoff = DateTime.UtcNow - retention;
            var kept = entries.Where(e => e.AppliedAtUtc >= cutoff).ToList();
            if (kept.Count != entries.Count)
            {
                await SaveAsync(kept);
                return kept;
            }
        }
        return entries;
    }

    private async Task SaveAsync(List<HistoryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
