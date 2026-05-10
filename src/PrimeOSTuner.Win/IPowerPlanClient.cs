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
    /// <summary>Runs powercfg with the given args and returns stdout; throws on non-zero exit.</summary>
    string RunPowercfg(string args);
}
