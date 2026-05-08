using System.Diagnostics;
using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class ProcessClientTests
{
    [Fact]
    public void TrimWorkingSet_does_not_throw_for_current_process()
    {
        var client = new ProcessClient();
        var pid = Process.GetCurrentProcess().Id;

        var act = () => client.TrimWorkingSet(pid);

        act.Should().NotThrow();
    }

    [Fact]
    public void TrimAllUserProcesses_returns_count_of_processes_attempted()
    {
        var client = new ProcessClient();

        var attempted = client.TrimAllUserProcesses();

        attempted.Should().BeGreaterThan(0);
    }
}
