using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Bloatware;

namespace PrimeOSTuner.UI.ViewModels;

public sealed class BloatwareItemRowVm : ObservableObject
{
    public BloatwareItem Item { get; }

    public string DisplayName => Item.Entry.DisplayName;
    public string AppxName => Item.Entry.AppxName;
    public string Category => Item.Entry.Category;
    public string TierLabel => Item.Entry.Tier.ToString();
    public string TierIcon => Item.Entry.Tier switch
    {
        SafetyTier.Safe => "✅",
        SafetyTier.Risky => "⚠",
        SafetyTier.Blocked => "🔒",
        _ => ""
    };
    public bool CanUninstall => Item.Entry.Tier != SafetyTier.Blocked;
    public string? RiskNote => Item.Entry.RiskNote;

    private string _statusText = "Installed";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public BloatwareItemRowVm(BloatwareItem item)
    {
        Item = item;
    }
}

public sealed class DesktopBloatRowVm
{
    public DesktopBloatHit Hit { get; }
    public DesktopBloatRowVm(DesktopBloatHit hit) { Hit = hit; }
    public string DisplayName => Hit.Entry.DisplayName;
    public string ProgramName => Hit.Program.DisplayName;
    public string Publisher => Hit.Program.Publisher ?? "";
    public string TierLabel => Hit.Entry.Tier.ToString();
    public string? RiskNote => Hit.Entry.RiskNote;
}

public partial class BloatwareViewModel : ObservableObject
{
    private readonly BloatwareDetector _detector;
    private readonly IInstalledProgramsClient _programs;
    private readonly IReadOnlyList<DesktopBloatwareCatalogEntry> _desktopCatalog;
    public ObservableCollection<BloatwareItemRowVm> Items { get; } = new();
    public ObservableCollection<DesktopBloatRowVm> DesktopItems { get; } = new();

    [ObservableProperty] private string _status = "Click Refresh to scan installed bloatware.";
    [ObservableProperty] private int _detectedCount;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasDesktopItems;

    public BloatwareViewModel(
        BloatwareDetector detector,
        IInstalledProgramsClient programs,
        IReadOnlyList<DesktopBloatwareCatalogEntry> desktopCatalog)
    {
        _detector = detector;
        _programs = programs;
        _desktopCatalog = desktopCatalog;
    }

    public async Task RefreshAsync()
    {
        IsScanning = true;
        Status = "Scanning installed packages…";
        try
        {
            var items = await _detector.DetectAsync();
            Items.Clear();
            foreach (var i in items) Items.Add(new BloatwareItemRowVm(i));

            // Desktop (Win32) programs — detection only; the action opens the program's own
            // uninstaller (registry scan runs off the UI thread; it touches many keys).
            var desktopHits = await Task.Run(() =>
                DesktopBloatwareDetector.Match(_programs.ListInstalled(), _desktopCatalog));
            DesktopItems.Clear();
            foreach (var hit in desktopHits) DesktopItems.Add(new DesktopBloatRowVm(hit));
            HasDesktopItems = DesktopItems.Count > 0;

            DetectedCount = items.Count + desktopHits.Count;
            Status = DetectedCount == 0
                ? "No known bloatware detected."
                : $"{items.Count} Store app(s) + {desktopHits.Count} desktop program(s) detected.";
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
