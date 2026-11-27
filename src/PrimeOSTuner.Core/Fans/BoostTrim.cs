using System.Text.Json;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Fans;

/// <summary>
/// The thermal governor's hands: turns turbo boost off and gives it back, one power scheme
/// at a time, remembering exactly what each scheme held.
///
/// Why per scheme: power settings belong to a scheme, not to the machine. The old trim
/// wrote "boost off" to every scheme that existed at that moment, then on restore wrote the
/// ACTIVE scheme's old value to every scheme. Two holes:
/// - a scheme created AFTER the trim was never trimmed. Game mode does exactly that: the
///   end of a game deletes Ultimate Performance (core.ultimate-performance's revert) and the
///   next launch duplicates a fresh one from the template, with boost at full — so a CPU
///   that was hot enough to be trimmed got full boost back the moment a game started.
/// - restore flattened every scheme to one value, so a plan the user had set differently
///   came back changed.
/// So: the ledger records each scheme's own value before we touch it; while trimmed, the
/// active scheme is checked every tick and trimmed too if it isn't (new plan, plan switch,
/// something rewrote the value); restore puts back each scheme's own value — and only on
/// schemes still holding our "off", so a later change by the user or a tweak wins.
///
/// The ledger is persisted through <c>persist</c> BEFORE any write, so a crash mid-trim
/// can always be undone on the next start.
/// </summary>
public sealed class BoostTrim
{
    private readonly IPowerPlanClient _power;
    private readonly Action<string?> _persist;
    private readonly Dictionary<Guid, int?> _original = new();

    private const string Sub = ThermalGovernor.SubProcessorGuid;
    private const string Setting = ThermalGovernor.PerfBoostModeGuid;

    /// <param name="persist">Receives the ledger as text whenever it changes, or null once
    /// nothing is trimmed any more (delete the marker).</param>
    public BoostTrim(IPowerPlanClient power, Action<string?> persist)
    {
        _power = power;
        _persist = persist;
    }

    /// <summary>True while at least one scheme holds a trim we owe back.</summary>
    public bool Active => _original.Count > 0;

    /// <summary>What each trimmed scheme held before (null = never set → Windows default).</summary>
    public IReadOnlyDictionary<Guid, int?> Original => _original;

    /// <summary>
    /// Trims boost on every scheme that has it on. Schemes already at "off" (Quiet CPU, or
    /// the user) are left alone and not recorded — nothing there is ours to give back.
    /// Returns false when there was nothing to trim.
    /// </summary>
    public bool Engage()
    {
        var toTrim = new List<Guid>();
        foreach (var plan in _power.ListPlans())
        {
            var current = Read(plan.Guid);
            if (current == ThermalGovernor.BoostDisabled) continue;
            if (!_original.ContainsKey(plan.Guid)) _original[plan.Guid] = current;
            toTrim.Add(plan.Guid);
        }
        if (toTrim.Count == 0) return Active;

        Persist();
        foreach (var g in toTrim) Write(g, ThermalGovernor.BoostDisabled);
        Reactivate();
        return true;
    }

    /// <summary>
    /// Called every tick while trimmed: if the ACTIVE scheme has boost on (a plan created or
    /// switched to since the trim, or the value rewritten), trim it too and remember what it
    /// held — unless we already hold its original. Returns true when it had to act.
    /// </summary>
    public bool FollowActivePlan()
    {
        if (!Active) return false;
        var active = _power.GetActivePlan().Guid;
        var current = Read(active);
        if (current == ThermalGovernor.BoostDisabled) return false;

        // A scheme we trimmed earlier keeps its FIRST recorded value: what it shows now was
        // written by someone else while we held it, not what the user chose.
        if (!_original.ContainsKey(active)) _original[active] = current;
        Persist();
        Write(active, ThermalGovernor.BoostDisabled);
        Reactivate();
        return true;
    }

    /// <summary>
    /// Gives each trimmed scheme back its own value. Skips schemes that no longer exist
    /// (Game mode deletes Ultimate Performance on exit) and schemes no longer at "off"
    /// (someone changed them since — theirs wins).
    /// </summary>
    public void Restore()
    {
        if (!Active) { _persist(null); return; }
        var existing = _power.ListPlans().Select(p => p.Guid).ToHashSet();
        foreach (var (scheme, original) in _original)
        {
            if (!existing.Contains(scheme)) continue;
            if (Read(scheme) != ThermalGovernor.BoostDisabled) continue;
            Write(scheme, original ?? ThermalGovernor.BoostWindowsDefault);
        }
        Reactivate();
        _original.Clear();
        _persist(null);
    }

    /// <summary>
    /// Crash recovery: undo a trim recorded by a previous run. Understands both the
    /// per-scheme ledger and the old marker format (one bare number that was written to
    /// every scheme — restored the old way, to every scheme).
    /// </summary>
    public void RecoverFromMarker(string markerText)
    {
        var text = markerText.Trim();
        if (int.TryParse(text, out var legacy))
        {
            _power.RestoreValueIndexPerScheme(Sub, Setting, new Dictionary<Guid, int?>(), legacy);
            _original.Clear();
            _persist(null);
            return;
        }
        _original.Clear();
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<Guid, int?>>(text);
            if (saved is not null)
                foreach (var (k, v) in saved) _original[k] = v;
        }
        catch { /* unreadable ledger: nothing safe to restore per scheme */ }

        if (_original.Count == 0)
        {
            // Unreadable: boost must not stay off forever — Windows default everywhere.
            _power.RestoreValueIndexPerScheme(Sub, Setting, new Dictionary<Guid, int?>(),
                ThermalGovernor.BoostWindowsDefault);
            _persist(null);
            return;
        }
        Restore();
    }

    private int? Read(Guid scheme)
    {
        try { return _power.GetSchemeSettingIndexFromRegistry(scheme, Sub, Setting); }
        catch { return null; }
    }

    private void Write(Guid scheme, int value)
    {
        try
        {
            _power.RunPowercfg($"/setacvalueindex {scheme:D} {Sub} {Setting} {value}");
            _power.RunPowercfg($"/setdcvalueindex {scheme:D} {Sub} {Setting} {value}");
        }
        catch { /* one scheme refusing must not abort the rest */ }
    }

    // Re-activating is what makes the running system pick a changed value up.
    private void Reactivate()
    {
        try { _power.RunPowercfg("/setactive SCHEME_CURRENT"); } catch { }
    }

    private void Persist() => _persist(JsonSerializer.Serialize(_original));
}
