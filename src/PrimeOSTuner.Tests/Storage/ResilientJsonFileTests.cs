using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Storage;
using Xunit;

namespace PrimeOSTuner.Tests.Storage;

public class ResilientJsonFileTests
{
    [Fact]
    public async Task ReadTextAsync_returns_null_when_file_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rjf-{Guid.NewGuid():N}.json");
        (await ResilientJsonFile.ReadTextAsync(path)).Should().BeNull();
    }

    [Fact]
    public async Task Write_then_read_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rjf-{Guid.NewGuid():N}.json");
        try
        {
            await ResilientJsonFile.WriteTextAsync(path, "{\"a\":1}");
            (await ResilientJsonFile.ReadTextAsync(path)).Should().Be("{\"a\":1}");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Heavy_concurrent_read_write_delete_never_throws_file_in_use()
    {
        // The whole reason this helper exists: concurrent access must not throw
        // "the process cannot access the file ... because it is being used by another process".
        var path = Path.Combine(Path.GetTempPath(), $"rjf-{Guid.NewGuid():N}.json");
        try
        {
            var tasks = new List<Task>();
            for (int i = 0; i < 60; i++)
            {
                tasks.Add(ResilientJsonFile.WriteTextAsync(path, $"{{\"n\":{i}}}"));
                tasks.Add(ResilientJsonFile.ReadTextAsync(path));
                if (i % 10 == 0) tasks.Add(ResilientJsonFile.DeleteAsync(path));
            }
            var act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Sync_write_then_read_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rjf-{Guid.NewGuid():N}.json");
        try
        {
            ResilientJsonFile.WriteText(path, "hello");
            ResilientJsonFile.ReadText(path).Should().Be("hello");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
