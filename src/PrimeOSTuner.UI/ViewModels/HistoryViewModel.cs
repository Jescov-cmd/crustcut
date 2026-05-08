using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.History;

namespace PrimeOSTuner.UI.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly TweakHistory _history;

    [ObservableProperty] private ObservableCollection<HistoryEntry> _entries = new();

    public HistoryViewModel(TweakHistory history) { _history = history; }

    public async Task LoadAsync()
    {
        var entries = await _history.LoadAsync();
        Entries = new ObservableCollection<HistoryEntry>(entries.Reverse());
    }
}
