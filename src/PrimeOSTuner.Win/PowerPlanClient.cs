using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PrimeOSTuner.Win;

public sealed class PowerPlanClient : IPowerPlanClient
{
    private static readonly Guid UltimatePerformanceTemplate =
        new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    public IReadOnlyList<PowerPlan> ListPlans()
    {
        var output = RunPowerCfg("/list");
        var plans = new List<PowerPlan>();
        var rx = new Regex(@"GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)");
        foreach (Match m in rx.Matches(output))
        {
            plans.Add(new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim()));
        }
        return plans;
    }

    public PowerPlan GetActivePlan()
    {
        var output = RunPowerCfg("/getactivescheme");
        var rx = new Regex(@"GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)");
        var m = rx.Match(output);
        if (!m.Success) throw new InvalidOperationException($"Could not parse: {output}");
        return new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim());
    }

    public void SetActivePlan(Guid planGuid) => RunPowerCfg($"/setactive {planGuid:D}");

    public Guid EnsureUltimatePerformancePlan()
    {
        var existing = ListPlans().FirstOrDefault(p =>
            p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Guid;

        var output = RunPowerCfg($"/duplicatescheme {UltimatePerformanceTemplate:D}");
        var rx = new Regex(@"([0-9a-fA-F-]{36})");
        var m = rx.Match(output);
        if (!m.Success) throw new InvalidOperationException($"Could not duplicate ultimate plan: {output}");
        return Guid.Parse(m.Value);
    }

    public void SetActiveAcValueIndex(string subgroup, string setting, int value)
    {
        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT {subgroup} {setting} {value}");
        RunPowerCfg("/setactive SCHEME_CURRENT");
    }

    public int? GetActiveAcValueIndex(string subgroup, string setting)
    {
        try
        {
            var output = RunPowerCfg($"/query SCHEME_CURRENT {subgroup} {setting}");
            var rx = new Regex(@"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
            var m = rx.Match(output);
            if (!m.Success) return null;
            return int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
        }
        catch { return null; }
    }

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && string.IsNullOrEmpty(output))
            throw new InvalidOperationException($"powercfg failed: {error}");
        return output;
    }
}
