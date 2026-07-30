using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Diagnosis;

namespace Crustcut.Presentation;

public sealed class FindingVm
{
    public Finding Finding { get; }
    public FindingVm(Finding finding) => Finding = finding;

    public string Title => Finding.Title;
    public string Detail => Finding.Detail;
    public string? FixSuggestion => Finding.FixSuggestion;
    public bool HasFix => !string.IsNullOrWhiteSpace(FixSuggestion);

    public bool IsProblem => Finding.Severity == FindingSeverity.Problem;
    public bool IsWarning => Finding.Severity == FindingSeverity.Warning;
    public bool IsPassed => Finding.Severity == FindingSeverity.Passed;

    public string SeverityLabel => Finding.Severity switch
    {
        FindingSeverity.Problem => "PROBLEM",
        FindingSeverity.Warning => "WARNING",
        _ => "OK",
    };
}

public partial class DiagnosisViewModel : ObservableObject
{
    private readonly DiagnosisService _service;

    public ObservableCollection<FindingVm> Findings { get; } = new();

    [ObservableProperty] private string _status = "Run a scan to check your system for problems.";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasRun;

    public DiagnosisViewModel(DiagnosisService service) => _service = service;

    public async Task ScanAsync()
    {
        IsScanning = true;
        Findings.Clear();
        try
        {
            // Progress is constructed HERE so it captures the UI context — its callback
            // then marshals back automatically while RunAsync's WMI/counter probes run on
            // a worker thread instead of freezing the page.
            var progress = new Progress<string>(m => Status = m);
            var results = await Task.Run(() => _service.RunAsync(progress));

            // Problems first, then warnings, then passes — the reason someone opens this page
            // is to find what's wrong, so what's wrong goes at the top.
            foreach (var f in results.OrderByDescending(f => f.Severity))
                Findings.Add(new FindingVm(f));

            var problems = results.Count(f => f.Severity == FindingSeverity.Problem);
            var warnings = results.Count(f => f.Severity == FindingSeverity.Warning);

            Status = problems == 0 && warnings == 0
                ? $"No problems found across {results.Count} checks."
                : $"{problems} problem(s), {warnings} warning(s) out of {results.Count} checks.";
            HasRun = true;
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
