using System.Text.Json;
using PrimeOSTuner.Core.Storage;

namespace PrimeOSTuner.Core.Profiles;

/// <summary>
/// Tracks tweaks that the user enabled which live in volatile, in-memory state and do
/// NOT survive a reboot on their own (e.g. the 0.5 ms timer resolution set via
/// NtSetTimerResolution). Crustcut autostarts and stays resident in the tray, so on each
/// launch it re-applies whatever is recorded here — making those tweaks effectively
/// persistent for the user who turned them on, without forcing them on anyone else.
/// </summary>
public sealed class SessionTweakStore
{
    private readonly string _path;
    private readonly System.Threading.SemaphoreSlim _rmw = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SessionTweakStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "session-tweaks.json");

    public async Task<IReadOnlyCollection<string>> LoadAsync()
    {
        var json = await ResilientJsonFile.ReadTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    public async Task AddAsync(string tweakId)
    {
        // Serialize the whole read-modify-write so two near-simultaneous toggles can't
        // each load the same set and clobber the other's change.
        await _rmw.WaitAsync();
        try
        {
            var set = new HashSet<string>(await LoadAsync(), StringComparer.OrdinalIgnoreCase);
            if (set.Add(tweakId)) await SaveAsync(set);
        }
        finally { _rmw.Release(); }
    }

    /// <summary>
    /// Backfills the enforce-set with everything currently detected as applied. This is
    /// what makes enforcement comprehensive for users who turned tweaks on in older builds
    /// (before per-toggle recording existed) or via "Apply All"/profiles — without it,
    /// Windows resetting something like Game Mode after a reboot left nothing to restore it.
    /// One load-modify-write for the whole batch.
    /// </summary>
    public async Task AddManyAsync(IEnumerable<string> tweakIds)
    {
        await _rmw.WaitAsync();
        try
        {
            var set = new HashSet<string>(await LoadAsync(), StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var id in tweakIds) changed |= set.Add(id);
            if (changed) await SaveAsync(set);
        }
        finally { _rmw.Release(); }
    }

    public async Task RemoveAsync(string tweakId)
    {
        await _rmw.WaitAsync();
        try
        {
            var set = new HashSet<string>(await LoadAsync(), StringComparer.OrdinalIgnoreCase);
            if (set.Remove(tweakId)) await SaveAsync(set);
        }
        finally { _rmw.Release(); }
    }

    private async Task SaveAsync(HashSet<string> set)
        => await ResilientJsonFile.WriteTextAsync(_path, JsonSerializer.Serialize(set, JsonOpts));
}

/// <summary>
/// IDs of tweaks whose effect is volatile (in-memory) and therefore needs to be
/// re-applied on each app launch to persist. Keep this list tiny and deliberate — most
/// tweaks write to the registry/power scheme and persist on their own.
/// </summary>
public static class SessionScopedTweaks
{
    public static readonly IReadOnlySet<string> Ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "game.timer-resolution",
    };

    public static bool IsSessionScoped(string tweakId) => Ids.Contains(tweakId);
}
