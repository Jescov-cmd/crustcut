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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public TweakHistory(string filePath)
    {
        _filePath = filePath;
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
        return JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts) ?? new List<HistoryEntry>();
    }

    private async Task SaveAsync(List<HistoryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
