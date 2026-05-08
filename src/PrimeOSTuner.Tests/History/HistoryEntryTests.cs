using System;
using System.Text.Json;
using FluentAssertions;
using PrimeOSTuner.Core.History;
using Xunit;

namespace PrimeOSTuner.Tests.History;

public class HistoryEntryTests
{
    [Fact]
    public void HistoryEntry_round_trips_through_json()
    {
        var entry = new HistoryEntry(
            Id: Guid.NewGuid(),
            TweakId: "core.junk-files",
            DisplayName: "Junk file cleanup",
            AppliedAtUtc: DateTime.UtcNow,
            UndoData: "{\"freed\":2048}",
            Reverted: false);

        var json = JsonSerializer.Serialize(entry);
        var round = JsonSerializer.Deserialize<HistoryEntry>(json);

        round.Should().NotBeNull();
        round!.Should().BeEquivalentTo(entry);
    }
}
