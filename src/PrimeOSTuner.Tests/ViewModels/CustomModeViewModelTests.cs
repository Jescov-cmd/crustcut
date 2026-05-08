using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class CustomModeViewModelTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid()}.json");
    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    private static Mock<ITweak> Stub(string id, string name)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(name);
        m.SetupGet(t => t.Description).Returns($"Description of {name}");
        return m;
    }

    [Fact]
    public async Task LoadAsync_marks_tweaks_already_in_custom_profile_as_selected()
    {
        var store = new CustomProfileStore(_path);
        await store.SaveAsync(new[] { "a" });
        var tweaks = new[] { Stub("a", "A").Object, Stub("b", "B").Object };

        var vm = new CustomModeViewModel(tweaks, store);
        await vm.LoadAsync();

        vm.Items.First(i => i.Id == "a").IsSelected.Should().BeTrue();
        vm.Items.First(i => i.Id == "b").IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_writes_only_selected_ids_to_store()
    {
        var store = new CustomProfileStore(_path);
        var tweaks = new[] { Stub("a", "A").Object, Stub("b", "B").Object };
        var vm = new CustomModeViewModel(tweaks, store);
        await vm.LoadAsync();

        vm.Items.First(i => i.Id == "b").IsSelected = true;
        await vm.SaveAsync();

        var loaded = await store.LoadAsync();
        loaded.TweakIds.Should().BeEquivalentTo(new[] { "b" });
    }
}
