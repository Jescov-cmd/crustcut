namespace PrimeOSTuner.Win;

public sealed record PowerPlan(Guid Guid, string Name);

public interface IPowerPlanClient
{
    IReadOnlyList<PowerPlan> ListPlans();
    PowerPlan GetActivePlan();
    void SetActivePlan(Guid planGuid);
    Guid EnsureUltimatePerformancePlan();
    /// <summary>Sets a powercfg value index on the active scheme (AC). Subgroup and setting are GUIDs or alias names like SUB_PROCESSOR / CPMINCORES.</summary>
    void SetActiveAcValueIndex(string subgroup, string setting, int value);
    /// <summary>Reads the AC index for a setting, or null if powercfg cannot return it.</summary>
    int? GetActiveAcValueIndex(string subgroup, string setting);
    /// <summary>
    /// Reads the active scheme's AC setting index directly from the registry by GUID.
    /// Needed for HIDDEN settings (e.g. CPU core parking / CPMINCORES) that
    /// <c>powercfg /query</c> refuses to return. Returns null if not explicitly set.
    /// </summary>
    int? GetActiveSchemeSettingIndexFromRegistry(string subgroupGuid, string settingGuid);
    /// <summary>Same read, but for a named scheme rather than whichever one is active.</summary>
    int? GetSchemeSettingIndexFromRegistry(Guid scheme, string subgroupGuid, string settingGuid);
    /// <summary>
    /// Writes an AC+DC value index into EVERY power scheme and returns what each one held
    /// before (null = the setting was never set on that scheme).
    ///
    /// Power settings belong to a scheme, not to the machine. Writing only to the active
    /// scheme means the tweak silently evaporates the moment the user picks a different
    /// power plan in Control Panel — which looks exactly like the app doing nothing, or
    /// worse, like switching to a *lower* power plan making the CPU hotter.
    /// </summary>
    IReadOnlyDictionary<Guid, int?> SetValueIndexOnAllSchemes(string subgroup, string setting, int value);
    /// <summary>Restores per-scheme values captured by <see cref="SetValueIndexOnAllSchemes"/>.</summary>
    void RestoreValueIndexPerScheme(string subgroup, string setting,
        IReadOnlyDictionary<Guid, int?> previous, int fallback);
    /// <summary>Runs powercfg with the given args and returns stdout; throws on non-zero exit.</summary>
    string RunPowercfg(string args);
}
