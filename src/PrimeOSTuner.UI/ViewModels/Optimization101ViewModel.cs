using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrimeOSTuner.Core.Education;

namespace PrimeOSTuner.UI.ViewModels;

/// <summary>
/// Drives the "Optimization 101" tab: a category filter, the filtered list of
/// guide cards, the selected guide, per-guide completion, and (for guides that
/// support it) a best-effort "currently on your PC" state badge.
/// </summary>
public partial class Optimization101ViewModel : ObservableObject
{
    private const string AllCategories = "All";

    private readonly GuideCompletionStore _completionStore;
    private readonly List<GuideTileViewModel> _allTiles;
    private readonly HashSet<string> _completed;

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<GuideTileViewModel> VisibleGuides { get; } = new();

    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private GuideTileViewModel? _selectedGuide;
    [ObservableProperty] private string _progressText = "";

    public Optimization101ViewModel(IReadOnlyList<Guide> guides, GuideCompletionStore completionStore)
    {
        _completionStore = completionStore;
        _completed = new HashSet<string>(
            completionStore.LoadAsync().GetAwaiter().GetResult(),
            StringComparer.OrdinalIgnoreCase);

        _allTiles = guides
            .OrderBy(g => g.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GuideTileViewModel(g, _completed.Contains(g.Id)))
            .ToList();

        Categories.Add(AllCategories);
        foreach (var category in guides
                     .Select(g => g.Category)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(category);
        }

        ApplyFilter(AllCategories);
        UpdateProgress();
    }

    [RelayCommand]
    private void ToggleCompleted()
    {
        if (SelectedGuide is null) return;

        SelectedGuide.IsCompleted = !SelectedGuide.IsCompleted;
        if (SelectedGuide.IsCompleted) _completed.Add(SelectedGuide.Guide.Id);
        else _completed.Remove(SelectedGuide.Guide.Id);

        _completionStore.SaveAsync(_completed).GetAwaiter().GetResult();
        UpdateProgress();
    }

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter(value);

    partial void OnSelectedGuideChanged(GuideTileViewModel? value)
    {
        if (value is null) return;
        value.StateBadge = "";

        // Only XMP/EXPO is cheaply detectable; other guides stay badge-free.
        if (value.Guide.Id == "enable-xmp-expo")
            _ = DetectMemoryStateAsync(value);
    }

    private void ApplyFilter(string? category)
    {
        VisibleGuides.Clear();
        foreach (var tile in _allTiles)
        {
            if (string.IsNullOrEmpty(category) || category == AllCategories
                || string.Equals(tile.Guide.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                VisibleGuides.Add(tile);
            }
        }
    }

    private void UpdateProgress()
        => ProgressText = $"{_allTiles.Count(t => t.IsCompleted)} of {_allTiles.Count} guides marked done";

    private static async Task DetectMemoryStateAsync(GuideTileViewModel tile)
    {
        var state = await Task.Run(SystemStateProbe.DetectMemoryProfile);
        tile.StateBadge = state switch
        {
            DetectedState.Enabled =>
                "On your PC: your memory looks like it's already running a profile above stock speed.",
            DetectedState.Disabled =>
                "On your PC: your memory looks like it's running at stock speed — this guide likely applies to you.",
            _ => "",
        };
    }
}

/// <summary>One guide rendered as a selectable card.</summary>
public partial class GuideTileViewModel : ObservableObject
{
    public Guide Guide { get; }

    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string _stateBadge = "";

    public GuideTileViewModel(Guide guide, bool isCompleted)
    {
        Guide = guide;
        _isCompleted = isCompleted;
    }

    public string Title => Guide.Title;
    public string Category => Guide.Category;
    public string Difficulty => Guide.Difficulty.ToString();
    public string RiskLabel => $"{Guide.Risk} risk";
    public string EstimatedTime => Guide.EstimatedTime;
    public string Body => Guide.MarkdownBody;
    public bool IsHighRisk => Guide.Risk == GuideRisk.High;

    public bool HasStateBadge => StateBadge.Length > 0;
    public string DoneLabel => IsCompleted ? "✓  Completed — click to undo" : "Mark as completed";

    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(DoneLabel));
    partial void OnStateBadgeChanged(string value) => OnPropertyChanged(nameof(HasStateBadge));
}
