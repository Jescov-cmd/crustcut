using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class RestorePointClientTests
{
    [Fact]
    public void IsAvailable_does_not_throw()
    {
        var client = new RestorePointClient();
        var act = () => client.IsAvailable();
        act.Should().NotThrow();
    }
}
