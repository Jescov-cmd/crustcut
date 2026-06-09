using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Persists the undo data needed to revert each applied tweak, keyed by tweak id, in a store
/// that does NOT expire and is not user-clearable.
///
/// Previously the only copy of this undo data lived in the History log — which the user can
/// clear and which now self-expires after 24h. That left applied tweaks un-revertable once the
/// history was gone (the toggle had no undo to restore, so turning it off silently did nothing).
/// This store keeps the revert data alive until the tweak is actually reverted.
/// </summary>
public sealed class PendingUndoStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _rmw = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public PendingUndoStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "pending-undo.json");

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync()
    {
        var json = await ResilientJsonFile.ReadTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return map is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<string?> GetAsync(string tweakId)
    {
        var map = await LoadAsync();
        return map.TryGetValue(tweakId, out var undo) ? undo : null;
    }

    /// <summary>
    /// Records the pristine undo for a tweak. Does NOT overwrite an existing entry: the first
    /// apply holds the pristine pre-Crustcut value, and a re-apply (which captures the already-
    /// applied value as its "previous") must not poison it.
    /// </summary>
    public async Task SetIfAbsentAsync(string tweakId, string? undoData)
    {
        if (string.IsNullOrEmpty(undoData)) return;
        await _rmw.WaitAsync();
        try
        {
            var map = new Dictionary<string, string>(await LoadAsync(), StringComparer.OrdinalIgnoreCase);
            if (!map.ContainsKey(tweakId))
            {
                map[tweakId] = undoData;
                await SaveAsync(map);
            }
        }
        finally { _rmw.Release(); }
    }

    public async Task RemoveAsync(string tweakId)
    {
        await _rmw.WaitAsync();
        try
        {
            var map = new Dictionary<string, string>(await LoadAsync(), StringComparer.OrdinalIgnoreCase);
            if (map.Remove(tweakId)) await SaveAsync(map);
        }
        finally { _rmw.Release(); }
    }

    private async Task SaveAsync(Dictionary<string, string> map)
        => await ResilientJsonFile.WriteTextAsync(_path, JsonSerializer.Serialize(map, JsonOpts));
}
