namespace PrimeOSTuner.Core.Diagnosis;

/// <summary>OS measurements the scan needs. Each returns null when it can't measure (rule stays silent).</summary>
public interface IDiagnosisProbes
{
    /// <summary>Average "% Processor Performance" while briefly loading the CPU (~2.5 s).</summary>
    Task<double?> SampleCpuPerformanceUnderLoadAsync(CancellationToken ct);
    Task<IReadOnlyList<ProcSample>> SampleTopProcessesAsync(CancellationToken ct);
    double? RamUsedPercent();
    (long Free, long Total)? SystemDriveSpace();
    string? ActivePowerPlanName();
    DateTime? NewestGpuDriverDateUtc();
}

public sealed class DiagnosisService
{
    private readonly IDiagnosisProbes _probes;
    public DiagnosisService(IDiagnosisProbes probes) { _probes = probes; }

    public async Task<IReadOnlyList<Finding>> RunAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        void Add(Finding? f) { if (f is not null) findings.Add(f); }

        progress?.Report("Checking CPU speed under load…");
        Add(DiagnosisRules.EvaluateCpuThrottle(await _probes.SampleCpuPerformanceUnderLoadAsync(ct)));

        progress?.Report("Looking for background hogs…");
        Add(DiagnosisRules.EvaluateBackgroundHogs(await _probes.SampleTopProcessesAsync(ct)));

        progress?.Report("Checking memory…");
        if (_probes.RamUsedPercent() is double ram) Add(DiagnosisRules.EvaluateRam(ram));

        progress?.Report("Checking disk space…");
        if (_probes.SystemDriveSpace() is var (free, total) && total > 0) Add(DiagnosisRules.EvaluateDisk(free, total));

        progress?.Report("Checking power plan…");
        Add(DiagnosisRules.EvaluatePowerPlan(_probes.ActivePowerPlanName()));

        progress?.Report("Checking GPU driver…");
        if (_probes.NewestGpuDriverDateUtc() is DateTime d) Add(DiagnosisRules.EvaluateGpuDriver(d, DateTime.UtcNow));

        // Problems first, then warnings, then passes.
        return findings.OrderByDescending(f => (int)f.Severity).ThenBy(f => f.Title).ToList();
    }
}
