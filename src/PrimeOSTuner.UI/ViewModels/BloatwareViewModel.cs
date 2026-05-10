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

public partial class BloatwareViewModel : ObservableObject
{
    private readonly BloatwareDetector _detector;
    public ObservableCollection<BloatwareItemRowVm> Items { get; } = new();

    [ObservableProperty] private string _status = "Click Refresh to scan installed bloatware.";
    [ObservableProperty] private int _detectedCount;
    [ObservableProperty] private bool _isScanning;

    public BloatwareViewModel(BloatwareDetector detector)
    {
        _detector = detector;
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
            DetectedCount = items.Count;
            Status = items.Count == 0
                ? "No known bloatware detected."
                : $"{items.Count} bloatware item(s) detected.";
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
