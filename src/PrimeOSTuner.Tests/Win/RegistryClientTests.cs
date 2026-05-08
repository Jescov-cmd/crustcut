using FluentAssertions;
using Microsoft.Win32;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class RegistryClientTests : IDisposable
{
    private const string SubKey = @"Software\PrimeOSTuner\TestArea";

    public RegistryClientTests()
    {
        Registry.CurrentUser.DeleteSubKeyTree(SubKey, throwOnMissingSubKey: false);
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(SubKey, false);

    [Fact]
    public void ReadString_returns_null_when_value_missing()
    {
        var client = new RegistryClient();
        client.ReadString(RegistryHive.CurrentUser, SubKey, "Missing").Should().BeNull();
    }

    [Fact]
    public void WriteString_creates_value_and_returns_backup_with_previous()
    {
        var client = new RegistryClient();
        using (var key = Registry.CurrentUser.CreateSubKey(SubKey)!)
            key.SetValue("Speed", "1");

        var backup = client.WriteString(RegistryHive.CurrentUser, SubKey, "Speed", "0");

        backup.PreviousValue.Should().Be("1");
        client.ReadString(RegistryHive.CurrentUser, SubKey, "Speed").Should().Be("0");
    }

    [Fact]
    public void RestoreFromBackup_returns_value_to_original()
    {
        var client = new RegistryClient();
        using (var key = Registry.CurrentUser.CreateSubKey(SubKey)!)
            key.SetValue("Speed", "1");
        var backup = client.WriteString(RegistryHive.CurrentUser, SubKey, "Speed", "0");

        client.RestoreFromBackup(backup);

        client.ReadString(RegistryHive.CurrentUser, SubKey, "Speed").Should().Be("1");
    }
}
