using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrimeOSTuner.Core.Diagnosis;
using PrimeOSTuner.Core.Sentinel;

namespace PrimeOSTuner.UI.Views;

public partial class DiagnosisView : UserControl
{
    public sealed record FindingRow(string Badge, Brush BadgeBrush, string Title, string Detail, string Fix, bool HasFix);
    public sealed record LiveRow(string Title, string Detail);

    private readonly DiagnosisService _service;
    private readonly ISentinelService _sentinel;
    private readonly ObservableCollection<FindingRow> _rows = new();
    private readonly ObservableCollection<LiveRow> _live = new();
    // De-dupe live problems by kind+detail so a problem persisting across ticks shows once.
    private readonly System.Collections.Generic.HashSet<string> _seenLive = new();

    public DiagnosisView(DiagnosisService service, ISentinelService sentinel)
    {
        InitializeComponent();
        _service = service;
        _sentinel = sentinel;
        FindingsList.ItemsSource = _rows;
        LiveList.ItemsSource = _live;
        // Accumulate problems Sentinel detects during play. Marshal to the UI thread —
        // Changed fires from the watcher's timer thread.
        _sentinel.Changed += (_, _) => Dispatcher.BeginInvoke(CollectLiveProblems);
        CollectLiveProblems();
    }

    private void CollectLiveProblems()
    {
        var game = _sentinel.WatchingGame;
        foreach (var p in _sentinel.Currently)
        {
            var key = $"{p.Kind}|{p.Detail}";
            if (!_seenLive.Add(key)) continue;
            var title = game is null ? FriendlyKind(p.Kind) : $"{FriendlyKind(p.Kind)} — while playing {game}";
            _live.Insert(0, new LiveRow(title, p.Detail));
        }
        while (_live.Count > 20) _live.RemoveAt(_live.Count - 1);
        LiveEmptyText.Visibility = _live.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FriendlyKind(ProblemKind kind) => kind switch
    {
        ProblemKind.CpuThrottled => "CPU throttled",
        ProblemKind.CpuSaturated => "CPU maxed out",
        ProblemKind.RamPressure => "RAM pressure",
        ProblemKind.VramOverhead => "GPU memory full",
        _ => kind.ToString(),
    };

    private async void RunClick(object sender, RoutedEventArgs e)
    {
        RunBtn.IsEnabled = false;
        _rows.Clear();
        try
        {
            var progress = new Progress<string>(s => ProgressText.Text = s);
            var findings = await _service.RunAsync(progress);
            ProgressText.Text = $"Done — {findings.Count(f => f.Severity == FindingSeverity.Problem)} problem(s), " +
                                $"{findings.Count(f => f.Severity == FindingSeverity.Warning)} warning(s).";
            foreach (var f in findings)
            {
                var (badge, brush) = f.Severity switch
                {
                    FindingSeverity.Problem => ("✖ PROBLEM", (Brush)FindResource("DangerBrush")),
                    FindingSeverity.Warning => ("⚠ WARNING", (Brush)FindResource("WarnBrush")),
                    _ => ("✔ OK", (Brush)FindResource("GoodBrush")),
                };
                _rows.Add(new FindingRow(badge, brush, f.Title, f.Detail, f.FixSuggestion ?? "", f.FixSuggestion is not null));
            }
        }
        catch (Exception ex)
        {
            ProgressText.Text = $"Scan failed: {ex.Message}";
        }
        finally { RunBtn.IsEnabled = true; }
    }
}
