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

        backup.PreviousString.Should().Be("1");
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

    // ── DWORD integration tests ─────────────────────────────────────────────

    [Fact]
    public void WriteDword_then_ReadDword_returns_the_value()
    {
        const string testSubKey = SubKey + @"\WriteDword_then_ReadDword_returns_the_value";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            var client = new RegistryClient();
            client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Counter", 42);

            client.ReadDword(RegistryHive.CurrentUser, testSubKey, "Counter").Should().Be(42);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void WriteDword_returns_backup_with_PreviousDword_when_value_existed_as_dword()
    {
        const string testSubKey = SubKey + @"\WriteDword_returns_backup_with_PreviousDword_when_value_existed_as_dword";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(testSubKey)!)
                key.SetValue("Throttle", 100, RegistryValueKind.DWord);

            var client = new RegistryClient();
            var backup = client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Throttle", 0);

            backup.PreviousDword.Should().Be(100);
            backup.PreviousKind.Should().Be(RegistryValueKind.DWord);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void WriteDword_returns_backup_with_Unknown_kind_when_value_did_not_exist()
    {
        const string testSubKey = SubKey + @"\WriteDword_returns_backup_with_Unknown_kind_when_value_did_not_exist";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            var client = new RegistryClient();
            var backup = client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Brand New", 7);

            backup.PreviousString.Should().BeNull();
            backup.PreviousDword.Should().BeNull();
            backup.PreviousKind.Should().Be(RegistryValueKind.Unknown);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void WriteDword_is_idempotent_when_value_already_matches()
    {
        const string testSubKey = SubKey + @"\WriteDword_is_idempotent_when_value_already_matches";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            var client = new RegistryClient();
            client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Flag", 0);

            // Re-writing the SAME value must succeed and report the current value as previous.
            // Guards the real-world case where Windows tamper-protects an already-correct value
            // (Win11 'TaskbarDa') and SetValue would throw "unauthorized operation" if we wrote
            // it again — which used to surface as a bogus "needs administrator" error.
            var backup = client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Flag", 0);

            backup.PreviousDword.Should().Be(0);
            backup.PreviousKind.Should().Be(RegistryValueKind.DWord);
            client.ReadDword(RegistryHive.CurrentUser, testSubKey, "Flag").Should().Be(0);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void RestoreFromBackup_restores_dword()
    {
        const string testSubKey = SubKey + @"\RestoreFromBackup_restores_dword";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(testSubKey)!)
                key.SetValue("Volume", 80, RegistryValueKind.DWord);

            var client = new RegistryClient();
            var backup = client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Volume", 0);
            client.RestoreFromBackup(backup);

            client.ReadDword(RegistryHive.CurrentUser, testSubKey, "Volume").Should().Be(80);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void RestoreFromBackup_deletes_value_when_it_did_not_previously_exist()
    {
        const string testSubKey = SubKey + @"\RestoreFromBackup_deletes_value_when_it_did_not_previously_exist";
        Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        try
        {
            var client = new RegistryClient();
            var backup = client.WriteDword(RegistryHive.CurrentUser, testSubKey, "Ephemeral", 99);
            client.RestoreFromBackup(backup);

            client.ReadDword(RegistryHive.CurrentUser, testSubKey, "Ephemeral").Should().BeNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testSubKey, throwOnMissingSubKey: false);
        }
    }
}
