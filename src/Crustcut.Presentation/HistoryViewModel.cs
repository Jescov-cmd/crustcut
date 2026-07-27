using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.History;

namespace Crustcut.Presentation;

public partial class HistoryViewModel : ObservableObject
{
    private readonly TweakHistory _history;

    [ObservableProperty] private ObservableCollection<HistoryEntry> _entries = new();
    [ObservableProperty] private string _status = "";

    public HistoryViewModel(TweakHistory history) => _history = history;

    public async Task LoadAsync()
    {
        var entries = await _history.LoadAsync();
        Entries = new ObservableCollection<HistoryEntry>(entries.Reverse());
        Status = Entries.Count == 0
            ? "Nothing changed yet. Anything you apply shows up here."
            : $"{Entries.Count} change(s), newest first.";
    }
}
