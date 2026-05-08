using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.ViewModels;

public partial class CustomModeItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public CustomModeItem(ITweak t)
    {
        Id = t.Id; DisplayName = t.DisplayName; Description = t.Description;
    }
}

public partial class CustomModeViewModel : ObservableObject
{
    private readonly IEnumerable<ITweak> _tweaks;
    private readonly CustomProfileStore _store;

    public ObservableCollection<CustomModeItem> Items { get; } = new();

    public CustomModeViewModel(IEnumerable<ITweak> tweaks, CustomProfileStore store)
    {
        _tweaks = tweaks;
        _store = store;
    }

    public async Task LoadAsync()
    {
        Items.Clear();
        var current = await _store.LoadAsync();
        var selected = new HashSet<string>(current.TweakIds);
        foreach (var t in _tweaks)
        {
            var item = new CustomModeItem(t) { IsSelected = selected.Contains(t.Id) };
            Items.Add(item);
        }
    }

    public Task SaveAsync()
    {
        var ids = Items.Where(i => i.IsSelected).Select(i => i.Id);
        return _store.SaveAsync(ids);
    }
}
