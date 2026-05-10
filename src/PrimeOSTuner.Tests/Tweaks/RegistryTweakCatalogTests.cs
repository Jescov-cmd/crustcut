using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RegistryTweakCatalogTests
{
    [Fact]
    public void Load_returns_empty_when_json_has_no_tweaks()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ \"tweaks\": [] }");
        try
        {
            var defs = RegistryTweakCatalog.LoadFromFile(path);
            defs.Should().BeEmpty();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_parses_one_dword_tweak()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "tweaks": [
                {
                  "id": "core.test",
                  "displayName": "Test",
                  "description": "Just a test",
                  "category": "system",
                  "requiresElevation": true,
                  "requiresReboot": false,
                  "hive": "LocalMachine",
                  "key": "SOFTWARE\\Test",
                  "valueName": "Foo",
                  "valueKind": "DWord",
                  "appliedData": "1",
                  "riskNote": null
                }
              ]
            }
            """);
        try
        {
            var defs = RegistryTweakCatalog.LoadFromFile(path);
            defs.Should().HaveCount(1);
            defs[0].Id.Should().Be("core.test");
            defs[0].ValueKind.Should().Be("DWord");
            defs[0].AppliedData.Should().Be("1");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_throws_when_id_is_duplicated()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "tweaks": [
                { "id": "x", "displayName": "A", "description": "", "category": "system",
                  "requiresElevation": false, "requiresReboot": false,
                  "hive": "CurrentUser", "key": "k", "valueName": "v",
                  "valueKind": "String", "appliedData": "0", "riskNote": null },
                { "id": "x", "displayName": "B", "description": "", "category": "system",
                  "requiresElevation": false, "requiresReboot": false,
                  "hive": "CurrentUser", "key": "k", "valueName": "v",
                  "valueKind": "String", "appliedData": "1", "riskNote": null }
              ]
            }
            """);
        try
        {
            var act = () => RegistryTweakCatalog.LoadFromFile(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate*x*");
        }
        finally { File.Delete(path); }
    }
}
