using FluentAssertions;
using PrimeOSTuner.Win.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

public class ProcessSuspendSafetyTests
{
    [Theory]
    [InlineData(@"NT AUTHORITY\SYSTEM")]
    [InlineData(@"NT AUTHORITY\LOCAL SERVICE")]
    [InlineData(@"NT AUTHORITY\NETWORK SERVICE")]
    [InlineData(@"NT SERVICE\TrustedInstaller")]
    public void IsSafeToSuspend_blocks_built_in_system_accounts(string owner)
    {
        ProcessSuspendSafety.IsSafeToSuspend(owner).Should().BeFalse();
    }

    [Theory]
    [InlineData(@"DESKTOP-ABC\jaxso")]
    [InlineData(@"MYPC\Admin")]
    public void IsSafeToSuspend_allows_regular_user_owned_processes(string owner)
    {
        ProcessSuspendSafety.IsSafeToSuspend(owner).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafeToSuspend_refuses_when_the_owner_is_unknown(string? owner)
    {
        ProcessSuspendSafety.IsSafeToSuspend(owner).Should().BeFalse();
    }
}
