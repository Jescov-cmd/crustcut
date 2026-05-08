using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PrimeOSTuner.Core.History;
using Xunit;

namespace PrimeOSTuner.Tests.History;

public class TweakHistoryTests : IDisposable
{
    private readonly string _tempPath;

    public TweakHistoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"primeos-history-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public async Task Append_creates_file_when_missing()
    {
        var history = new TweakHistory(_tempPath);

        await history.AppendAsync(new HistoryEntry(
            Guid.NewGuid(), "x", "X", DateTime.UtcNow, null, false));

        File.Exists(_tempPath).Should().BeTrue();
    }

    [Fact]
    public async Task Load_returns_entries_in_append_order()
    {
        var history = new TweakHistory(_tempPath);
        var first = new HistoryEntry(Guid.NewGuid(), "a", "A", DateTime.UtcNow, null, false);
        var second = new HistoryEntry(Guid.NewGuid(), "b", "B", DateTime.UtcNow.AddSeconds(1), null, false);

        await history.AppendAsync(first);
        await history.AppendAsync(second);

        var entries = (await history.LoadAsync()).ToList();
        entries.Should().HaveCount(2);
        entries[0].TweakId.Should().Be("a");
        entries[1].TweakId.Should().Be("b");
    }

    [Fact]
    public async Task MarkReverted_updates_entry_in_place()
    {
        var history = new TweakHistory(_tempPath);
        var entry = new HistoryEntry(Guid.NewGuid(), "a", "A", DateTime.UtcNow, "u", false);
        await history.AppendAsync(entry);

        await history.MarkRevertedAsync(entry.Id);

        var loaded = (await history.LoadAsync()).Single();
        loaded.Reverted.Should().BeTrue();
    }
}
