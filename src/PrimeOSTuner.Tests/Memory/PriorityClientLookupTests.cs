using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

[Trait("Category", "Integration")]
public class PriorityClientLookupTests
{
    private static string Self => Environment.ProcessPath!;

    [Fact]
    public void Finds_this_process_by_its_image_path()
    {
        new PriorityClient().FindPidsForExe(Self).Should().Contain(Environment.ProcessId);
    }

    [Fact]
    public void Path_match_ignores_case()
    {
        new PriorityClient().FindPidsForExe(Self.ToUpperInvariant())
            .Should().Contain(Environment.ProcessId);
    }

    [Fact]
    public void A_batch_answers_every_lookup_from_one_snapshot()
    {
        var client = new PriorityClient();
        using (client.BeginBatch())
        {
            client.FindPidsForExe(Self).Should().Contain(Environment.ProcessId);
            client.FindPidsForExes(new[] { Self, @"C:\does\not\exist.exe" })
                .Should().Contain(Environment.ProcessId);
        }
        // Outside the batch lookups are live again.
        client.FindPidsForExe(Self).Should().Contain(Environment.ProcessId);
    }

    [Fact]
    public void One_pid_resolves_to_its_own_image_path()
    {
        new PriorityClient().ImagePathOf(Environment.ProcessId)
            .Should().BeEquivalentTo(Environment.ProcessPath);
    }
}
