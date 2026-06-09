using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class PendingUndoStoreTests : IDisposable
{
    private readonly string _path;

    public PendingUndoStoreTests()
        => _path = Path.Combine(Path.GetTempPath(), $"primeos-undo-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public async Task Set_then_Get_roundtrips()
    {
        var store = new PendingUndoStore(_path);
        await store.SetIfAbsentAsync("a", "undo-a");
        (await store.GetAsync("a")).Should().Be("undo-a");
    }

    [Fact]
    public async Task SetIfAbsent_does_not_overwrite_pristine_undo()
    {
        var store = new PendingUndoStore(_path);
        await store.SetIfAbsentAsync("a", "pristine");
        await store.SetIfAbsentAsync("a", "poisoned");   // a re-apply must not clobber it
        (await store.GetAsync("a")).Should().Be("pristine");
    }

    [Fact]
    public async Task Remove_clears_the_entry()
    {
        var store = new PendingUndoStore(_path);
        await store.SetIfAbsentAsync("a", "undo-a");
        await store.RemoveAsync("a");
        (await store.GetAsync("a")).Should().BeNull();
    }

    [Fact]
    public async Task Undo_survives_a_reload_independent_of_history()
    {
        var store = new PendingUndoStore(_path);
        await store.SetIfAbsentAsync("a", "undo-a");

        var reloaded = new PendingUndoStore(_path);   // new instance = new "session"
        (await reloaded.GetAsync("a")).Should().Be("undo-a");
    }

    [Fact]
    public async Task Get_missing_returns_null()
    {
        var store = new PendingUndoStore(_path);
        (await store.GetAsync("nope")).Should().BeNull();
    }
}
