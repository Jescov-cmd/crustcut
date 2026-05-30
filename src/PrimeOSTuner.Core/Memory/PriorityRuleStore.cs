using System.IO;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Memory;

public sealed class PriorityRuleStore
{
    private readonly string _filePath;
    // Serializes all access process-wide (this store is a DI singleton). Without it, the
    // UI saving while another save/load races — e.g. rapid toggles, "Apply Recommended",
    // or a Re-scan — collided on the file and threw "being used by another process".
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PriorityRuleStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner",
        "priority-rules.json");

    public async Task<IReadOnlyList<PriorityRule>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<PriorityRule>();
            var json = await ReadWithRetryAsync();
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<PriorityRule>();
            return JsonSerializer.Deserialize<List<PriorityRule>>(json, Opts)
                ?? new List<PriorityRule>();
        }
        catch (JsonException)
        {
            return Array.Empty<PriorityRule>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<PriorityRule> rules)
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(rules, Opts);
            await WriteWithRetryAsync(json);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Share ReadWrite + brief retries so a transient external lock (antivirus, indexer,
    // backup) surfaces as a short wait rather than a crash dialog.
    private async Task<string> ReadWithRetryAsync()
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return await sr.ReadToEndAsync();
            }
            catch (IOException) when (attempt < 8)
            {
                await Task.Delay(75);
            }
        }
    }

    private async Task WriteWithRetryAsync(string json)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs);
                await sw.WriteAsync(json);
                return;
            }
            catch (IOException) when (attempt < 8)
            {
                await Task.Delay(75);
            }
        }
    }
}
