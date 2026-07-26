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
}
