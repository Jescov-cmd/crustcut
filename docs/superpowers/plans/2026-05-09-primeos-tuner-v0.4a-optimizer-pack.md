# PrimeOS Tuner v0.4a — Optimizer Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ~30 system-level optimizers across FPS/Latency, Network, System, Privacy, and Power categories using a data-driven `RegistryTweak` class plus a small set of custom tweaks for the cases that need real logic.

**Architecture:** A new `RegistryTweak` class implements `ITweak` once and is parameterized by rows loaded from `tweaks.json`. Custom classes (~10 of them) handle non-trivial cases — services, multi-key, AppX, NIC drivers, power plans. The Optimize tab gains three new filter chips: System, Privacy, Power.

**Tech Stack:** .NET 9 (net9.0-windows), C#, WPF, Microsoft.Win32 registry APIs, `System.ServiceProcess.ServiceController`, `System.Management` (WMI), xUnit + Moq + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-05-09-primeos-tuner-v0.4-optimizer-pack-design.md`

---

## File Structure

**Create:**
- `src/PrimeOSTuner.Core/Tweaks/RegistryTweak.cs` — data-driven `ITweak` implementation
- `src/PrimeOSTuner.Core/Tweaks/RegistryTweakDefinition.cs` — record describing one row
- `src/PrimeOSTuner.Core/Tweaks/RegistryTweakCatalog.cs` — loads `tweaks.json`
- `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json` — ~25 simple tweak rows
- `src/PrimeOSTuner.Core/Tweaks/ServiceDisableTweak.cs` — service-disable `ITweak`
- `src/PrimeOSTuner.Core/Tweaks/UltimatePerformanceTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/HibernationTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/TelemetryDisableTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/CortanaDisableTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/NicPowerManagementTweak.cs`
- `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakCatalogTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/ServiceDisableTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/UltimatePerformanceTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/HibernationTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/TelemetryDisableTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/CortanaDisableTweakTests.cs`

**Modify:**
- `src/PrimeOSTuner.Win/IRegistryClient.cs` — add DWORD read/write
- `src/PrimeOSTuner.Win/RegistryClient.cs` — implement DWORD methods
- `src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj` — copy `tweaks.json` to output
- `src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs` — add new tweak IDs
- `src/PrimeOSTuner.UI/Views/OptimizeView.xaml` — add System/Privacy/Power chips, risk badge
- `src/PrimeOSTuner.UI/Views/OptimizeView.xaml.cs` — extend `CategoryFor` mapping
- `src/PrimeOSTuner.UI/App.xaml.cs` — register new tweaks in DI

**Delete:** (after confirming JSON migration works)
- `src/PrimeOSTuner.Core/Tweaks/MouseAccelTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/SystemResponsivenessTweak.cs`
- `src/PrimeOSTuner.Core/Tweaks/NetworkThrottlingIndexTweak.cs`
- `src/PrimeOSTuner.Tests/Tweaks/MouseAccelTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/SystemResponsivenessTweakTests.cs`
- `src/PrimeOSTuner.Tests/Tweaks/NetworkThrottlingIndexTweakTests.cs`

(`NagleAlgorithmTweak` and `PerAppGpuPreferenceTweak` stay — they have logic.)

---

## Task 1: Extend `IRegistryClient` to support DWORD values

**Why:** Many of the new tweaks set DWORD registry values. The existing client only writes strings.

**Files:**
- Modify: `src/PrimeOSTuner.Win/IRegistryClient.cs`
- Modify: `src/PrimeOSTuner.Win/RegistryClient.cs`

- [ ] **Step 1: Add a DWORD-aware backup record + interface methods**

Replace the contents of `src/PrimeOSTuner.Win/IRegistryClient.cs`:

```csharp
using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed record RegistryBackup(
    RegistryHive Hive,
    string SubKey,
    string ValueName,
    string? PreviousString,        // null if value didn't exist OR previous was DWORD
    int? PreviousDword = null,     // null if previous was string OR didn't exist
    RegistryValueKind PreviousKind = RegistryValueKind.Unknown
);

public interface IRegistryClient
{
    string? ReadString(RegistryHive hive, string subKey, string valueName);
    int? ReadDword(RegistryHive hive, string subKey, string valueName);
    RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue);
    RegistryBackup WriteDword(RegistryHive hive, string subKey, string valueName, int newValue);
    void RestoreFromBackup(RegistryBackup backup);
}
```

Note: `RegistryBackup` gains optional fields. Existing call sites that construct `new RegistryBackup(hive, key, name, prev)` keep working — `PreviousString` is the third positional arg, the rest default.

- [ ] **Step 2: Implement DWORD methods in `RegistryClient`**

Open `src/PrimeOSTuner.Win/RegistryClient.cs`. Add these methods to the class (alongside the existing string ones):

```csharp
public int? ReadDword(RegistryHive hive, string subKey, string valueName)
{
    using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
    using var key = root.OpenSubKey(subKey);
    if (key is null) return null;
    var v = key.GetValue(valueName);
    if (v is null) return null;
    return v switch
    {
        int i => i,
        long l => unchecked((int)l),
        _ => int.TryParse(v.ToString(), out var parsed) ? parsed : null
    };
}

public RegistryBackup WriteDword(RegistryHive hive, string subKey, string valueName, int newValue)
{
    using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
    using var key = root.CreateSubKey(subKey, writable: true)
        ?? throw new InvalidOperationException($"Could not open or create {hive}\\{subKey}");
    var prev = key.GetValue(valueName);
    var prevKind = prev is null ? RegistryValueKind.Unknown : key.GetValueKind(valueName);
    string? prevString = null;
    int? prevDword = null;
    if (prev is int i) prevDword = i;
    else if (prev is not null) prevString = prev.ToString();
    key.SetValue(valueName, newValue, RegistryValueKind.DWord);
    return new RegistryBackup(hive, subKey, valueName, prevString, prevDword, prevKind);
}
```

Update `RestoreFromBackup` so it knows how to restore both kinds:

```csharp
public void RestoreFromBackup(RegistryBackup backup)
{
    using var root = RegistryKey.OpenBaseKey(backup.Hive, RegistryView.Default);
    using var key = root.CreateSubKey(backup.SubKey, writable: true);
    if (key is null) return;

    if (backup.PreviousKind == RegistryValueKind.Unknown
        && backup.PreviousString is null
        && backup.PreviousDword is null)
    {
        // Value didn't exist before — delete it.
        if (key.GetValue(backup.ValueName) is not null)
            key.DeleteValue(backup.ValueName, throwOnMissingValue: false);
        return;
    }

    if (backup.PreviousDword is int d)
    {
        key.SetValue(backup.ValueName, d, RegistryValueKind.DWord);
    }
    else if (backup.PreviousString is string s)
    {
        var kind = backup.PreviousKind == RegistryValueKind.Unknown
            ? RegistryValueKind.String
            : backup.PreviousKind;
        key.SetValue(backup.ValueName, s, kind);
    }
}
```

- [ ] **Step 3: Build and run all tests to confirm nothing regressed**

Run from the repo root:

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Expected: build succeeds, all existing tests still pass. (The added optional parameters on `RegistryBackup` don't break existing constructor calls.)

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.Win/IRegistryClient.cs src/PrimeOSTuner.Win/RegistryClient.cs
git commit -m "feat(win): add DWORD read/write to IRegistryClient

RegistryBackup gains optional PreviousDword and PreviousKind fields so
RestoreFromBackup can put back the original kind (DWORD or string)."
```

---

## Task 2: Define the `RegistryTweakDefinition` record

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/RegistryTweakDefinition.cs`

- [ ] **Step 1: Create the record**

```csharp
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

public sealed record RegistryTweakDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Category,                 // "fps" | "network" | "system" | "privacy" | "power"
    bool RequiresElevation,
    bool RequiresReboot,
    string Hive,                     // "LocalMachine" | "CurrentUser" | "ClassesRoot"
    string Key,
    string ValueName,
    string ValueKind,                // "DWord" | "String"
    string AppliedData,              // for DWord, parseable as int; for String, used as-is
    string? RiskNote                 // optional inline warning shown on the tile
)
{
    public RegistryHive ParsedHive => Hive switch
    {
        "LocalMachine" => RegistryHive.LocalMachine,
        "CurrentUser" => RegistryHive.CurrentUser,
        "ClassesRoot" => RegistryHive.ClassesRoot,
        _ => throw new InvalidOperationException($"Unsupported hive: {Hive}")
    };

    public RegistryValueKind ParsedKind => ValueKind switch
    {
        "DWord" => RegistryValueKind.DWord,
        "String" => RegistryValueKind.String,
        _ => throw new InvalidOperationException($"Unsupported value kind: {ValueKind}")
    };
}
```

- [ ] **Step 2: Build to confirm it compiles**

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj
```

Expected: success.

- [ ] **Step 3: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/RegistryTweakDefinition.cs
git commit -m "feat(core): add RegistryTweakDefinition record"
```

---

## Task 3: Implement `RegistryTweak` (TDD)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/RegistryTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RegistryTweakTests
{
    private static RegistryTweakDefinition DwordDef(string applied = "1") => new(
        Id: "test.dword-tweak",
        DisplayName: "Test DWORD",
        Description: "x",
        Category: "system",
        RequiresElevation: true,
        RequiresReboot: false,
        Hive: "LocalMachine",
        Key: @"SOFTWARE\Test",
        ValueName: "Foo",
        ValueKind: "DWord",
        AppliedData: applied,
        RiskNote: null
    );

    private static RegistryTweakDefinition StringDef(string applied = "0") => new(
        Id: "test.string-tweak",
        DisplayName: "Test STRING",
        Description: "x",
        Category: "system",
        RequiresElevation: true,
        RequiresReboot: false,
        Hive: "CurrentUser",
        Key: @"Control Panel\Test",
        ValueName: "Bar",
        ValueKind: "String",
        AppliedData: applied,
        RiskNote: null
    );

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_dword_value_matches()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo"))
                .Returns(1);
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_NotApplied_when_dword_value_differs()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo"))
                .Returns(0);
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_string_value_matches()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, @"Control Panel\Test", "Bar"))
                .Returns("0");
        var tweak = new RegistryTweak(StringDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ApplyAsync_writes_dword_and_returns_serializable_undo()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo", 1))
                .Returns(new RegistryBackup(
                    RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo",
                    PreviousString: null, PreviousDword: 0, PreviousKind: RegistryValueKind.DWord));
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().NotBeNull().And.Contain("Foo");
    }

    [Fact]
    public async Task ApplyAsync_writes_string_and_returns_serializable_undo()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, @"Control Panel\Test", "Bar", "0"))
                .Returns(new RegistryBackup(
                    RegistryHive.CurrentUser, @"Control Panel\Test", "Bar", "1"));
        var tweak = new RegistryTweak(StringDef(), registry.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().NotBeNull().And.Contain("Bar");
    }

    [Fact]
    public async Task RevertAsync_restores_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo", 1))
                .Returns(new RegistryBackup(
                    RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo",
                    PreviousString: null, PreviousDword: 0, PreviousKind: RegistryValueKind.DWord));
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);
        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Once);
    }

    [Fact]
    public void Identity_fields_come_from_definition()
    {
        var tweak = new RegistryTweak(DwordDef(), new Mock<IRegistryClient>().Object);
        tweak.Id.Should().Be("test.dword-tweak");
        tweak.DisplayName.Should().Be("Test DWORD");
        tweak.RequiresElevation.Should().BeTrue();
        tweak.IsDestructive.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail (no implementation yet)**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~RegistryTweakTests"
```

Expected: compile error or test failures because `RegistryTweak` doesn't exist yet.

- [ ] **Step 3: Implement `RegistryTweak`**

Create `src/PrimeOSTuner.Core/Tweaks/RegistryTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RegistryTweak : ITweak
{
    private readonly RegistryTweakDefinition _def;
    private readonly IRegistryClient _registry;

    public RegistryTweak(RegistryTweakDefinition def, IRegistryClient registry)
    {
        _def = def;
        _registry = registry;
    }

    public string Id => _def.Id;
    public string DisplayName => _def.DisplayName;
    public string Description => _def.Description;
    public bool RequiresElevation => _def.RequiresElevation;
    public bool IsDestructive => false;
    public bool RequiresReboot => _def.RequiresReboot;

    public string Category => _def.Category;
    public string? RiskNote => _def.RiskNote;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        if (_def.ParsedKind == RegistryValueKind.DWord)
        {
            var current = _registry.ReadDword(_def.ParsedHive, _def.Key, _def.ValueName);
            if (current is null) return Task.FromResult(TweakState.NotApplied);
            var expected = ParseDword(_def.AppliedData);
            return Task.FromResult(current == expected ? TweakState.Applied : TweakState.NotApplied);
        }
        else
        {
            var current = _registry.ReadString(_def.ParsedHive, _def.Key, _def.ValueName);
            return Task.FromResult(string.Equals(current, _def.AppliedData, StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied : TweakState.NotApplied);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        RegistryBackup backup;
        if (_def.ParsedKind == RegistryValueKind.DWord)
        {
            var value = ParseDword(_def.AppliedData);
            backup = _registry.WriteDword(_def.ParsedHive, _def.Key, _def.ValueName, value);
        }
        else
        {
            backup = _registry.WriteString(_def.ParsedHive, _def.Key, _def.ValueName, _def.AppliedData);
        }
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<RegistryBackup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _registry.RestoreFromBackup(backup);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set {_def.Hive}\\{_def.Key}\\{_def.ValueName} to '{_def.AppliedData}' ({_def.ValueKind}).");

    private static int ParseDword(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return unchecked((int)Convert.ToUInt32(raw, 16));
        return int.Parse(raw);
    }
}
```

- [ ] **Step 4: Run tests and confirm all pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~RegistryTweakTests"
```

Expected: 7 passed.

- [ ] **Step 5: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/RegistryTweak.cs src/PrimeOSTuner.Tests/Tweaks/RegistryTweakTests.cs
git commit -m "feat(core): add RegistryTweak data-driven ITweak

Loads a RegistryTweakDefinition and implements probe/apply/revert
generically for both DWORD and string registry values."
```

---

## Task 4: Build the empty `tweaks.json` catalog + loader (TDD)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`
- Create: `src/PrimeOSTuner.Core/Tweaks/RegistryTweakCatalog.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakCatalogTests.cs`
- Modify: `src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj`

- [ ] **Step 1: Write the catalog tests**

Create `src/PrimeOSTuner.Tests/Tweaks/RegistryTweakCatalogTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests and confirm they fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~RegistryTweakCatalogTests"
```

Expected: compile error — `RegistryTweakCatalog` doesn't exist.

- [ ] **Step 3: Implement `RegistryTweakCatalog`**

Create `src/PrimeOSTuner.Core/Tweaks/RegistryTweakCatalog.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Tweaks;

public static class RegistryTweakCatalog
{
    private sealed class Wrapper
    {
        [JsonPropertyName("tweaks")]
        public List<RegistryTweakDefinition> Tweaks { get; set; } = new();
    }

    public static IReadOnlyList<RegistryTweakDefinition> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Tweak catalog not found at {path}");

        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var wrapper = JsonSerializer.Deserialize<Wrapper>(json, opts)
            ?? throw new InvalidOperationException("Tweak catalog JSON is empty or invalid.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in wrapper.Tweaks)
        {
            if (!seen.Add(t.Id))
                throw new InvalidOperationException($"Tweak catalog has duplicate id: {t.Id}");
        }

        return wrapper.Tweaks;
    }

    public static string DefaultPath()
    {
        var dir = AppContext.BaseDirectory;
        return Path.Combine(dir, "Tweaks", "catalog", "tweaks.json");
    }
}
```

- [ ] **Step 4: Create an empty `tweaks.json`**

Create `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`:

```json
{
  "tweaks": []
}
```

- [ ] **Step 5: Mark `tweaks.json` as content that copies to output**

Open `src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj`. Find the existing `<ItemGroup>` blocks (or add a new one) and add:

```xml
<ItemGroup>
  <None Update="Tweaks\catalog\tweaks.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

This must propagate to anything that references the project — when `PrimeOSTuner.UI` builds, the file ends up next to the .exe under `Tweaks\catalog\tweaks.json`.

- [ ] **Step 6: Run tests and confirm pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~RegistryTweakCatalogTests"
```

Expected: 3 passed.

- [ ] **Step 7: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/RegistryTweakCatalog.cs src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json src/PrimeOSTuner.Tests/Tweaks/RegistryTweakCatalogTests.cs src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj
git commit -m "feat(core): add RegistryTweakCatalog JSON loader + empty catalog"
```

---

## Task 5: Wire `RegistryTweakCatalog` into DI and add the first two FPS & Latency tweaks

This task validates the whole pipeline: data file → loader → DI → Optimize tab. The two new tweaks here also ship as the first real catalog entries.

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Add the two new FPS & Latency rows to `tweaks.json`**

Replace the file contents with:

```json
{
  "tweaks": [
    {
      "id": "core.win32-priority-separation",
      "displayName": "Favor foreground apps (Win32 Priority Separation)",
      "description": "Boosts CPU priority for the active window. Helps responsiveness in games.",
      "category": "fps",
      "requiresElevation": true,
      "requiresReboot": true,
      "hive": "LocalMachine",
      "key": "SYSTEM\\CurrentControlSet\\Control\\PriorityControl",
      "valueName": "Win32PrioritySeparation",
      "valueKind": "DWord",
      "appliedData": "26",
      "riskNote": null
    },
    {
      "id": "core.startup-delay",
      "displayName": "Disable explorer startup delay",
      "description": "Removes Windows' artificial startup delay so apps can launch sooner after login.",
      "category": "fps",
      "requiresElevation": false,
      "requiresReboot": true,
      "hive": "CurrentUser",
      "key": "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize",
      "valueName": "StartupDelayInMSec",
      "valueKind": "DWord",
      "appliedData": "0",
      "riskNote": null
    }
  ]
}
```

- [ ] **Step 2: Register the catalog in DI**

Open `src/PrimeOSTuner.UI/App.xaml.cs`. Inside `ConfigureServices(s => { ... })`, find the section that registers individual tweaks (around line 78-87, where `s.AddSingleton<MouseAccelTweak>()` etc. live). Just BELOW that block, add:

```csharp
// Registry-driven tweak catalog (data file → many ITweak instances)
s.AddSingleton<IReadOnlyList<RegistryTweak>>(sp =>
{
    var registry = sp.GetRequiredService<IRegistryClient>();
    var defs = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath());
    return defs.Select(d => new RegistryTweak(d, registry)).ToList();
});
```

Then update the `IEnumerable<ITweak>` aggregator (around line 126-153). Find the `return new ITweak[] { ... };` block and change it to concatenate the catalog. Replace:

```csharp
return new ITweak[]
{
    sp.GetRequiredService<PowerPlanTweak>(),
    // ... all the existing entries ...
    perAppFactory(gamePaths),
};
```

with:

```csharp
var custom = new ITweak[]
{
    sp.GetRequiredService<PowerPlanTweak>(),
    sp.GetRequiredService<RamCleanerTweak>(),
    sp.GetRequiredService<DnsFlushTweak>(),
    sp.GetRequiredService<WindowsUpdateCacheTweak>(),
    sp.GetRequiredService<DriverHealthCheckTweak>(),
    sp.GetRequiredService<DriverStoreCleanupTweak>(),
    sp.GetRequiredService<SafeRegistryCleanupTweak>(),
    sp.GetRequiredService<MouseAccelTweak>(),
    sp.GetRequiredService<TimerResolutionTweak>(),
    sp.GetRequiredService<GameModeTweak>(),
    sp.GetRequiredService<HwGpuSchedulingTweak>(),
    sp.GetRequiredService<NagleAlgorithmTweak>(),
    sp.GetRequiredService<NetworkThrottlingIndexTweak>(),
    sp.GetRequiredService<SystemResponsivenessTweak>(),
    sp.GetRequiredService<CpuCoreParkingTweak>(),
    perAppFactory(gamePaths),
};
var catalog = sp.GetRequiredService<IReadOnlyList<RegistryTweak>>();
return custom.Concat(catalog).ToArray();
```

- [ ] **Step 3: Build and run the app to verify**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Expected: build succeeds; all tests pass.

Manually launch:

```powershell
dotnet run --project src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

Open the Optimize tab. The two new tweaks should appear as toggles (under FPS & Latency). Toggle one ON, then OFF, confirm no exceptions.

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): wire RegistryTweak catalog into DI; add 2 FPS tweaks

- Win32 Priority Separation (favor foreground)
- Disable explorer startup delay"
```

---

## Task 6: Migrate three existing simple tweaks into `tweaks.json`

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`
- Delete: `src/PrimeOSTuner.Core/Tweaks/MouseAccelTweak.cs`
- Delete: `src/PrimeOSTuner.Core/Tweaks/SystemResponsivenessTweak.cs`
- Delete: `src/PrimeOSTuner.Core/Tweaks/NetworkThrottlingIndexTweak.cs`
- Delete: `src/PrimeOSTuner.Tests/Tweaks/MouseAccelTweakTests.cs`
- Delete: `src/PrimeOSTuner.Tests/Tweaks/SystemResponsivenessTweakTests.cs`
- Delete: `src/PrimeOSTuner.Tests/Tweaks/NetworkThrottlingIndexTweakTests.cs`

**Important:** keep the existing tweak `id` strings (`game.mouse-accel`, `game.system-responsiveness`, `game.network-throttling`) so the history table and any user undo data continues to resolve. The `Id` field in `tweaks.json` is the migrated value.

**Note about `MouseAccelTweak`:** the original wrote three values in one apply. `RegistryTweak` writes one value per definition, so we'll create three rows for it (sharing the prefix but each with a distinct id). The history will show three entries when the user toggles the "Disable mouse acceleration" group, but practically the three appear together because they all live under FPS & Latency.

Actually, a cleaner approach: keep `MouseAccelTweak` AS A CUSTOM TWEAK (it groups three values into one user-facing toggle, which is correct UX). Migrate only `SystemResponsivenessTweak` and `NetworkThrottlingIndexTweak`, both of which are single-value writes.

- [ ] **Step 1: Add the two single-value migrations to `tweaks.json`**

Append two entries to `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json` (inside the `tweaks` array, after the existing two):

```json
{
  "id": "game.system-responsiveness",
  "displayName": "Maximize game CPU priority",
  "description": "Stops Windows from reserving 20% for background tasks.",
  "category": "fps",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile",
  "valueName": "SystemResponsiveness",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "game.network-throttling",
  "displayName": "Remove network throttling",
  "description": "Lets games use the full network all the time.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile",
  "valueName": "NetworkThrottlingIndex",
  "valueKind": "DWord",
  "appliedData": "-1",
  "riskNote": null
}
```

(`-1` is `0xffffffff` as a signed int32 — the same bit pattern.)

- [ ] **Step 2: Remove the corresponding lines from DI in `App.xaml.cs`**

In `App.xaml.cs`:
1. Delete the lines `s.AddSingleton<NetworkThrottlingIndexTweak>();` and `s.AddSingleton<SystemResponsivenessTweak>();`.
2. In the `IEnumerable<ITweak>` aggregator's `custom` array, remove the entries `sp.GetRequiredService<NetworkThrottlingIndexTweak>(),` and `sp.GetRequiredService<SystemResponsivenessTweak>(),`.

- [ ] **Step 3: Delete the now-orphaned source and test files**

```powershell
git rm src/PrimeOSTuner.Core/Tweaks/SystemResponsivenessTweak.cs
git rm src/PrimeOSTuner.Core/Tweaks/NetworkThrottlingIndexTweak.cs
git rm src/PrimeOSTuner.Tests/Tweaks/SystemResponsivenessTweakTests.cs
git rm src/PrimeOSTuner.Tests/Tweaks/NetworkThrottlingIndexTweakTests.cs
```

- [ ] **Step 4: Build and run all tests**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Expected: build clean (no orphan references), all tests pass.

- [ ] **Step 5: Smoke test the UI**

```powershell
dotnet run --project src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

Confirm "Maximize game CPU priority" and "Remove network throttling" still show as toggles. Toggle each ON then OFF.

- [ ] **Step 6: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "refactor(core): migrate SystemResponsiveness + NetworkThrottling to RegistryTweak"
```

---

## Task 7: Add the new Network registry tweaks (5 entries)

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`

- [ ] **Step 1: Add five new Network tweaks to `tweaks.json`**

Append inside the `tweaks` array:

```json
{
  "id": "core.tcp-ack-frequency",
  "displayName": "TCP ACK Frequency = 1",
  "description": "Sends TCP acknowledgements immediately. Slightly lower latency.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters",
  "valueName": "TcpAckFrequency",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
},
{
  "id": "core.tcp-delivery-acceleration",
  "displayName": "TCP Delivery Acceleration",
  "description": "Speeds up small-packet delivery.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters",
  "valueName": "TCPNoDelay",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
},
{
  "id": "core.qos-bandwidth",
  "displayName": "Disable reserved QoS bandwidth",
  "description": "Frees the 20% bandwidth Windows holds back for QoS.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\Psched",
  "valueName": "NonBestEffortLimit",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.netbios-disable",
  "displayName": "Disable NetBIOS over TCP/IP",
  "description": "Removes legacy NetBIOS broadcast traffic. Faster network for most home networks.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Services\\NetBT\\Parameters",
  "valueName": "NodeType",
  "valueKind": "DWord",
  "appliedData": "2",
  "riskNote": null
},
{
  "id": "core.ipv6-disable",
  "displayName": "Disable IPv6 (advanced)",
  "description": "Forces IPv4-only networking.",
  "category": "network",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters",
  "valueName": "DisabledComponents",
  "valueKind": "DWord",
  "appliedData": "255",
  "riskNote": "May break some apps and VPNs that require IPv6."
}
```

- [ ] **Step 2: Build and test**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Expected: success, all tests pass.

- [ ] **Step 3: Smoke test**

Launch the app, switch to Optimize tab, click the Network chip, verify all 5 new entries appear.

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json
git commit -m "feat(core): add 5 new Network registry tweaks"
```

---

## Task 8: Implement `ServiceDisableTweak` (TDD)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/ServiceDisableTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/ServiceDisableTweakTests.cs`

**Why a separate class instead of registry rows:** disabling a service involves stopping it (`ServiceController.Stop`) AND setting startup type to Disabled (registry write). Two-step + ordering matters + can fail mid-way. Worth its own class.

- [ ] **Step 1: Create a thin abstraction over `ServiceController`**

Create `src/PrimeOSTuner.Core/Tweaks/IServiceClient.cs`:

```csharp
namespace PrimeOSTuner.Core.Tweaks;

public sealed record ServiceState(bool Exists, string CurrentStartType, bool IsRunning);

public interface IServiceClient
{
    ServiceState Read(string serviceName);
    void SetStartTypeDisabled(string serviceName);
    void SetStartType(string serviceName, string startType);  // "Auto", "Manual", "Disabled"
    void Stop(string serviceName);
}
```

Then create `src/PrimeOSTuner.Core/Tweaks/ServiceClient.cs` (concrete impl):

```csharp
using System.ServiceProcess;
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class ServiceClient : IServiceClient
{
    public ServiceState Read(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            // Trigger an actual query to detect non-existence.
            _ = sc.Status;
            var startType = ReadStartType(serviceName);
            return new ServiceState(true, startType, sc.Status == ServiceControllerStatus.Running);
        }
        catch (InvalidOperationException) { return new ServiceState(false, "Unknown", false); }
    }

    public void SetStartTypeDisabled(string serviceName) => SetStartType(serviceName, "Disabled");

    public void SetStartType(string serviceName, string startType)
    {
        // Set via registry — works regardless of whether the service is running.
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
        if (key is null) return;
        var dword = startType switch
        {
            "Boot" => 0,
            "System" => 1,
            "Auto" => 2,
            "Manual" => 3,
            "Disabled" => 4,
            _ => throw new ArgumentException($"Unknown start type: {startType}", nameof(startType))
        };
        key.SetValue("Start", dword, RegistryValueKind.DWord);
    }

    public void Stop(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
        catch { /* swallow — best-effort */ }
    }

    private static string ReadStartType(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        if (key is null) return "Unknown";
        var v = key.GetValue("Start");
        return v switch
        {
            int i => i switch
            {
                0 => "Boot", 1 => "System", 2 => "Auto", 3 => "Manual", 4 => "Disabled", _ => "Unknown"
            },
            _ => "Unknown"
        };
    }
}
```

- [ ] **Step 2: Write tests for `ServiceDisableTweak`**

Create `src/PrimeOSTuner.Tests/Tweaks/ServiceDisableTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class ServiceDisableTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_service_is_disabled_and_stopped()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Disabled", false));
        var tweak = new ServiceDisableTweak(
            id: "core.sysmain-disable",
            displayName: "Disable SysMain",
            description: "x",
            category: "system",
            serviceName: "SysMain",
            riskNote: null,
            client: svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_Unknown_when_service_does_not_exist()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("Bogus")).Returns(new ServiceState(false, "Unknown", false));
        var tweak = new ServiceDisableTweak(
            "x", "x", "x", "system", "Bogus", null, svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Unknown);
    }

    [Fact]
    public async Task ApplyAsync_stops_service_and_disables_start_type()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Auto", true));
        var tweak = new ServiceDisableTweak(
            "core.sysmain-disable", "x", "x", "system", "SysMain", null, svc.Object);

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        svc.Verify(s => s.Stop("SysMain"), Times.Once);
        svc.Verify(s => s.SetStartTypeDisabled("SysMain"), Times.Once);
        result.UndoData.Should().Contain("Auto");
    }

    [Fact]
    public async Task RevertAsync_restores_previous_start_type()
    {
        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("SysMain")).Returns(new ServiceState(true, "Auto", true));
        var tweak = new ServiceDisableTweak(
            "core.sysmain-disable", "x", "x", "system", "SysMain", null, svc.Object);
        var apply = await tweak.ApplyAsync();

        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        svc.Verify(s => s.SetStartType("SysMain", "Auto"), Times.Once);
    }
}
```

- [ ] **Step 3: Run tests, confirm fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~ServiceDisableTweakTests"
```

Expected: compile error.

- [ ] **Step 4: Implement `ServiceDisableTweak`**

Create `src/PrimeOSTuner.Core/Tweaks/ServiceDisableTweak.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class ServiceDisableTweak : ITweak
{
    private readonly string _serviceName;
    private readonly IServiceClient _client;

    public ServiceDisableTweak(
        string id,
        string displayName,
        string description,
        string category,
        string serviceName,
        string? riskNote,
        IServiceClient client)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        _serviceName = serviceName;
        RiskNote = riskNote;
        _client = client;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public string? RiskNote { get; }
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    private sealed record Backup(string PreviousStartType);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        if (!s.Exists) return Task.FromResult(TweakState.Unknown);
        return Task.FromResult(s.CurrentStartType == "Disabled" && !s.IsRunning
            ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        if (!s.Exists)
            return Task.FromResult(TweakResult.Failure($"Service '{_serviceName}' does not exist."));
        var backup = new Backup(s.CurrentStartType);
        _client.Stop(_serviceName);
        _client.SetStartTypeDisabled(_serviceName);
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<Backup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _client.SetStartType(_serviceName, backup.PreviousStartType);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        return Task.FromResult(s.Exists
            ? $"Will stop the '{_serviceName}' service and set startup type to Disabled (currently {s.CurrentStartType})."
            : $"Service '{_serviceName}' does not exist on this system.");
    }
}
```

- [ ] **Step 5: Run tests, confirm pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~ServiceDisableTweakTests"
```

Expected: 4 passed.

- [ ] **Step 6: Register `IServiceClient` in DI**

In `src/PrimeOSTuner.UI/App.xaml.cs`, in the Win-layer registration block, add:

```csharp
s.AddSingleton<IServiceClient, ServiceClient>();
```

- [ ] **Step 7: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/IServiceClient.cs src/PrimeOSTuner.Core/Tweaks/ServiceClient.cs src/PrimeOSTuner.Core/Tweaks/ServiceDisableTweak.cs src/PrimeOSTuner.Tests/Tweaks/ServiceDisableTweakTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add ServiceDisableTweak for service-based optimizers"
```

---

## Task 9: Add System category — service disables + 3 registry tweaks

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Add 3 System category registry tweaks to `tweaks.json`**

Append inside the `tweaks` array:

```json
{
  "id": "core.werror-reporting",
  "displayName": "Disable Windows Error Reporting",
  "description": "Stops Windows from sending crash reports to Microsoft.",
  "category": "system",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting",
  "valueName": "Disabled",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
},
{
  "id": "core.game-dvr-disable",
  "displayName": "Disable Game DVR / background recording",
  "description": "Stops Windows from background-recording games. Frees CPU/disk.",
  "category": "system",
  "requiresElevation": false,
  "requiresReboot": false,
  "hive": "CurrentUser",
  "key": "System\\GameConfigStore",
  "valueName": "GameDVR_Enabled",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.fullscreen-optimizations",
  "displayName": "Disable fullscreen optimizations (system-wide)",
  "description": "Forces classic exclusive fullscreen for all games.",
  "category": "system",
  "requiresElevation": false,
  "requiresReboot": false,
  "hive": "CurrentUser",
  "key": "System\\GameConfigStore",
  "valueName": "GameDVR_FSEBehaviorMode",
  "valueKind": "DWord",
  "appliedData": "2",
  "riskNote": null
}
```

- [ ] **Step 2: Add System service-disable tweaks in DI**

In `App.xaml.cs`, just below the `s.AddSingleton<IServiceClient, ServiceClient>();` line, add three named ServiceDisableTweak factories:

```csharp
s.AddSingleton<IEnumerable<ServiceDisableTweak>>(sp =>
{
    var client = sp.GetRequiredService<IServiceClient>();
    return new[]
    {
        new ServiceDisableTweak(
            id: "core.sysmain-disable",
            displayName: "Disable Superfetch / SysMain",
            description: "Recommended for SSDs. Stops Windows pre-loading apps into memory.",
            category: "system",
            serviceName: "SysMain",
            riskNote: null,
            client: client),
        new ServiceDisableTweak(
            id: "core.search-indexing-tune",
            displayName: "Disable Windows Search indexing service",
            description: "Reduces background disk activity. Slows Start menu search.",
            category: "system",
            serviceName: "WSearch",
            riskNote: "Reduces Start menu search speed.",
            client: client),
        new ServiceDisableTweak(
            id: "core.connected-user-experiences",
            displayName: "Disable Connected User Experiences telemetry service",
            description: "Stops the DiagTrack-related Connected User Experiences and Telemetry service.",
            category: "system",
            serviceName: "DiagTrack",
            riskNote: null,
            client: client),
    };
});
```

In the `IEnumerable<ITweak>` aggregator, append the service tweaks too. Change the final return line to:

```csharp
var catalog = sp.GetRequiredService<IReadOnlyList<RegistryTweak>>();
var services = sp.GetRequiredService<IEnumerable<ServiceDisableTweak>>();
return custom.Concat(catalog).Concat(services).ToArray();
```

- [ ] **Step 3: Build, test, smoke test**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
dotnet run --project src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

Click Optimize. Verify 6 System-category items show up (3 registry + 3 service).

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add System category — 3 registry tweaks + 3 service disables"
```

---

## Task 10: Add Privacy category — 6 registry tweaks

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`

- [ ] **Step 1: Append 6 Privacy entries to `tweaks.json`**

```json
{
  "id": "core.ceip-disable",
  "displayName": "Disable Customer Experience Improvement Program",
  "description": "Stops anonymous usage data collection.",
  "category": "privacy",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows",
  "valueName": "CEIPEnable",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.activity-history",
  "displayName": "Disable Activity History",
  "description": "Stops Windows from tracking apps and files you've used.",
  "category": "privacy",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\System",
  "valueName": "EnableActivityFeed",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.advertising-id",
  "displayName": "Disable Advertising ID",
  "description": "Stops apps from using a shared ad-tracking identifier.",
  "category": "privacy",
  "requiresElevation": false,
  "requiresReboot": false,
  "hive": "CurrentUser",
  "key": "Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",
  "valueName": "Enabled",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.location-tracking",
  "displayName": "Disable Location tracking",
  "description": "Stops apps from accessing your physical location.",
  "category": "privacy",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Sensor\\Overrides\\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}",
  "valueName": "SensorPermissionState",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.feedback-diagnostics",
  "displayName": "Disable Feedback & Diagnostics",
  "description": "Suppresses Windows feedback prompts and diagnostic notifications.",
  "category": "privacy",
  "requiresElevation": false,
  "requiresReboot": false,
  "hive": "CurrentUser",
  "key": "Software\\Microsoft\\Siuf\\Rules",
  "valueName": "NumberOfSIUFInPeriod",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.typing-personalization",
  "displayName": "Disable typing/inking personalization",
  "description": "Stops Windows from sending typing samples to Microsoft.",
  "category": "privacy",
  "requiresElevation": false,
  "requiresReboot": false,
  "hive": "CurrentUser",
  "key": "Software\\Microsoft\\InputPersonalization",
  "valueName": "RestrictImplicitTextCollection",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
}
```

- [ ] **Step 2: Build, test, smoke test**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Smoke test: launch app, click the Privacy chip (which we'll add in Task 14, but the entries should still show under "All" and have category=privacy in the data).

- [ ] **Step 3: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json
git commit -m "feat(core): add Privacy category — 6 registry tweaks"
```

---

## Task 11: Implement `TelemetryDisableTweak` (custom — multi-key + service)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/TelemetryDisableTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/TelemetryDisableTweakTests.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

**Why a custom class:** disables three registry keys *and* stops the DiagTrack service. Sequencing and partial-failure handling matter.

- [ ] **Step 1: Write tests**

Create `src/PrimeOSTuner.Tests/Tweaks/TelemetryDisableTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TelemetryDisableTweakTests
{
    [Fact]
    public async Task ApplyAsync_writes_three_registry_keys_and_disables_diagtrack()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Auto", true));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection", "AllowTelemetry", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "DoNotShowFeedbackNotifications", 1), Times.Once);
        svc.Verify(s => s.Stop("DiagTrack"), Times.Once);
        svc.Verify(s => s.SetStartTypeDisabled("DiagTrack"), Times.Once);
    }

    [Fact]
    public async Task RevertAsync_restores_all_three_backups_and_resets_service()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Auto", true));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        reg.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
        svc.Verify(s => s.SetStartType("DiagTrack", "Auto"), Times.Once);
    }

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_telemetry_is_zero_and_service_disabled()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry")).Returns(0);

        var svc = new Mock<IServiceClient>();
        svc.Setup(s => s.Read("DiagTrack")).Returns(new ServiceState(true, "Disabled", false));

        var tweak = new TelemetryDisableTweak(reg.Object, svc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
```

- [ ] **Step 2: Run tests, confirm fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~TelemetryDisableTweakTests"
```

- [ ] **Step 3: Implement `TelemetryDisableTweak`**

Create `src/PrimeOSTuner.Core/Tweaks/TelemetryDisableTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class TelemetryDisableTweak : ITweak
{
    private readonly IRegistryClient _registry;
    private readonly IServiceClient _service;

    public TelemetryDisableTweak(IRegistryClient registry, IServiceClient service)
    {
        _registry = registry;
        _service = service;
    }

    public string Id => "core.telemetry-disable";
    public string DisplayName => "Disable Windows telemetry";
    public string Description => "Disables telemetry registry policies and stops the DiagTrack service.";
    public string Category => "privacy";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    private sealed record Backup(List<RegistryBackup> Registry, string ServiceStartType);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var policy = _registry.ReadDword(
            RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
            "AllowTelemetry");
        var svc = _service.Read("DiagTrack");
        return Task.FromResult(policy == 0 && svc.CurrentStartType == "Disabled"
            ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var registryBackups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                "AllowTelemetry", 0),
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                "AllowTelemetry", 0),
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                "DoNotShowFeedbackNotifications", 1),
        };
        var prevSvc = _service.Read("DiagTrack");
        _service.Stop("DiagTrack");
        _service.SetStartTypeDisabled("DiagTrack");
        var backup = new Backup(registryBackups, prevSvc.CurrentStartType);
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<Backup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backup.Registry) _registry.RestoreFromBackup(b);
        _service.SetStartType("DiagTrack", backup.ServiceStartType);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will set 3 telemetry registry policies to 0 and disable the DiagTrack service.");
}
```

- [ ] **Step 4: Run tests, confirm pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~TelemetryDisableTweakTests"
```

Expected: 3 passed.

- [ ] **Step 5: Register in DI**

In `App.xaml.cs`, register the tweak and add it to the aggregator:

```csharp
s.AddSingleton<TelemetryDisableTweak>();
```

In the `custom` array, add: `sp.GetRequiredService<TelemetryDisableTweak>(),`

- [ ] **Step 6: Build, smoke test, commit**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Core/Tweaks/TelemetryDisableTweak.cs src/PrimeOSTuner.Tests/Tweaks/TelemetryDisableTweakTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add TelemetryDisableTweak (3 keys + DiagTrack service)"
```

---

## Task 12: Implement `CortanaDisableTweak` (custom — multi-key)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/CortanaDisableTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/CortanaDisableTweakTests.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Write tests**

Create `src/PrimeOSTuner.Tests/Tweaks/CortanaDisableTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class CortanaDisableTweakTests
{
    [Fact]
    public async Task ApplyAsync_writes_three_cortana_policy_keys()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var tweak = new CortanaDisableTweak(reg.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "AllowCortana", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "DisableWebSearch", 1), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "ConnectedSearchUseWeb", 0), Times.Once);
    }

    [Fact]
    public async Task RevertAsync_restores_all_three_backups()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var tweak = new CortanaDisableTweak(reg.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        reg.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
    }
}
```

- [ ] **Step 2: Implement**

Create `src/PrimeOSTuner.Core/Tweaks/CortanaDisableTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CortanaDisableTweak : ITweak
{
    private const string PolicyKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search";
    private readonly IRegistryClient _registry;

    public CortanaDisableTweak(IRegistryClient registry) { _registry = registry; }

    public string Id => "core.cortana-disable";
    public string DisplayName => "Disable Cortana";
    public string Description => "Disables Cortana voice assistant and web-search policies.";
    public string Category => "privacy";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var allow = _registry.ReadDword(RegistryHive.LocalMachine, PolicyKey, "AllowCortana");
        return Task.FromResult(allow == 0 ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "AllowCortana", 0),
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "DisableWebSearch", 1),
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "ConnectedSearchUseWeb", 0),
        };
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backups)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backups = JsonSerializer.Deserialize<List<RegistryBackup>>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backups) _registry.RestoreFromBackup(b);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set 3 values under HKLM\\{PolicyKey} to disable Cortana.");
}
```

- [ ] **Step 3: Run tests, confirm pass; register; build; commit**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~CortanaDisableTweakTests"
```

In `App.xaml.cs`: add `s.AddSingleton<CortanaDisableTweak>();` and append `sp.GetRequiredService<CortanaDisableTweak>(),` to the `custom` array.

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Core/Tweaks/CortanaDisableTweak.cs src/PrimeOSTuner.Tests/Tweaks/CortanaDisableTweakTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add CortanaDisableTweak"
```

---

## Task 13: Implement `UltimatePerformanceTweak` (custom — `powercfg`)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/UltimatePerformanceTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/UltimatePerformanceTweakTests.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

**Approach:** wraps the `IPowerPlanClient` (already exists) plus a process call to `powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61` to summon the hidden Ultimate Performance plan. The well-known GUID is the canonical Ultimate Performance scheme.

- [ ] **Step 1: Add a `RunPowercfg` method to `IPowerPlanClient`**

Open `src/PrimeOSTuner.Win/IPowerPlanClient.cs`. Add this method to the interface:

```csharp
string RunPowercfg(string args);  // returns stdout; throws on non-zero exit
```

Open `src/PrimeOSTuner.Win/PowerPlanClient.cs` and add the implementation alongside the existing methods:

```csharp
public string RunPowercfg(string args)
{
    var psi = new ProcessStartInfo("powercfg.exe", args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0)
        throw new InvalidOperationException($"powercfg {args} failed: {stderr.Trim()}");
    return stdout;
}
```

(`PowerPlanClient` likely already imports `System.Diagnostics`. If not, add `using System.Diagnostics;` at the top.)

- [ ] **Step 2: Write tests for `UltimatePerformanceTweak`**

Create `src/PrimeOSTuner.Tests/Tweaks/UltimatePerformanceTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class UltimatePerformanceTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_powercfg_list_contains_ultimate_guid()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61  (Ultimate Performance) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_NotApplied_when_ultimate_not_in_list()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task ApplyAsync_runs_duplicatescheme_with_ultimate_guid()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61"))
           .Returns("Power Scheme GUID: 11111111-1111-1111-1111-111111111111  (Ultimate Performance)");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task RevertAsync_deletes_the_duplicated_plan()
    {
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        await tweak.RevertAsync("\"11111111-1111-1111-1111-111111111111\"");
        ppc.Verify(p => p.RunPowercfg("/delete 11111111-1111-1111-1111-111111111111"), Times.Once);
    }
}
```

- [ ] **Step 3: Implement**

Create `src/PrimeOSTuner.Core/Tweaks/UltimatePerformanceTweak.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class UltimatePerformanceTweak : ITweak
{
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private static readonly Regex GuidRx = new("(?<guid>[0-9a-f-]{36})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IPowerPlanClient _power;

    public UltimatePerformanceTweak(IPowerPlanClient power) { _power = power; }

    public string Id => "core.ultimate-performance";
    public string DisplayName => "Enable Ultimate Performance power plan";
    public string Description => "Adds Microsoft's hidden Ultimate Performance power plan (does not switch to it).";
    public string Category => "power";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var list = _power.RunPowercfg("/list");
            return Task.FromResult(list.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied : TweakState.NotApplied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var output = _power.RunPowercfg($"/duplicatescheme {UltimateGuid}");
        var match = GuidRx.Match(output);
        if (!match.Success)
            return Task.FromResult(TweakResult.Failure("powercfg did not return a new GUID."));
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(match.Groups["guid"].Value)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var guid = JsonSerializer.Deserialize<string>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _power.RunPowercfg($"/delete {guid}");
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'powercfg /duplicatescheme' to add Ultimate Performance.");
}
```

- [ ] **Step 4: Test, register, commit**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~UltimatePerformanceTweakTests"
```

In `App.xaml.cs`: add `s.AddSingleton<UltimatePerformanceTweak>();` and append to `custom` array.

```powershell
dotnet build src/PrimeOSTuner.sln && dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Win/IPowerPlanClient.cs src/PrimeOSTuner.Win/PowerPlanClient.cs src/PrimeOSTuner.Core/Tweaks/UltimatePerformanceTweak.cs src/PrimeOSTuner.Tests/Tweaks/UltimatePerformanceTweakTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add UltimatePerformanceTweak (powercfg /duplicatescheme)"
```

---

## Task 14: Implement `HibernationTweak` (custom — `powercfg /h`)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/HibernationTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/HibernationTweakTests.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Write tests**

Create `src/PrimeOSTuner.Tests/Tweaks/HibernationTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class HibernationTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_HiberbootEnabled_is_zero()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(Microsoft.Win32.RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled")).Returns(0);
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ApplyAsync_runs_powercfg_h_off()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(Microsoft.Win32.RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled")).Returns(1);
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        ppc.Verify(p => p.RunPowercfg("/h off"), Times.Once);
        result.UndoData.Should().Contain("1");
    }

    [Fact]
    public async Task RevertAsync_runs_powercfg_h_on_when_previously_enabled()
    {
        var reg = new Mock<IRegistryClient>();
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        await tweak.RevertAsync("1");
        ppc.Verify(p => p.RunPowercfg("/h on"), Times.Once);
    }
}
```

- [ ] **Step 2: Implement**

Create `src/PrimeOSTuner.Core/Tweaks/HibernationTweak.cs`:

```csharp
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class HibernationTweak : ITweak
{
    private readonly IRegistryClient _registry;
    private readonly IPowerPlanClient _power;

    public HibernationTweak(IRegistryClient registry, IPowerPlanClient power)
    {
        _registry = registry;
        _power = power;
    }

    public string Id => "core.hibernation-disable";
    public string DisplayName => "Disable Hibernation";
    public string Description => "Frees ~8 GB on disk. Sleep still works; only hibernation is removed.";
    public string Category => "power";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadDword(RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled");
        return Task.FromResult(v == 0 ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var prev = _registry.ReadDword(RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled") ?? 1;
        _power.RunPowercfg("/h off");
        return Task.FromResult(TweakResult.Success(prev.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (int.TryParse(undoData, out var prev) && prev == 1)
            _power.RunPowercfg("/h on");
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'powercfg /h off' to disable hibernation and remove hiberfil.sys.");
}
```

- [ ] **Step 3: Test, register, commit**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~HibernationTweakTests"
```

In `App.xaml.cs`: register and add to aggregator (`s.AddSingleton<HibernationTweak>();` + add to `custom` array).

```powershell
dotnet build src/PrimeOSTuner.sln && dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Core/Tweaks/HibernationTweak.cs src/PrimeOSTuner.Tests/Tweaks/HibernationTweakTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add HibernationTweak (powercfg /h off)"
```

---

## Task 15: Add Power category — 4 registry tweaks

**Files:**
- Modify: `src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json`

- [ ] **Step 1: Append 4 Power tweaks**

```json
{
  "id": "core.usb-selective-suspend",
  "displayName": "Disable USB selective suspend",
  "description": "Stops Windows from putting USB devices to sleep. Better for gaming peripherals.",
  "category": "power",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Services\\USB",
  "valueName": "DisableSelectiveSuspend",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
},
{
  "id": "core.pcie-aspm-disable",
  "displayName": "Disable PCIe link-state power management",
  "description": "Keeps PCIe devices (GPUs, NVMe) at full power.",
  "category": "power",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Control\\Power\\PowerSettings\\501a4d13-42af-4429-9fd1-a8218c268e20\\ee12f906-d277-404b-b6da-e5fa1a576df5",
  "valueName": "ACSettingIndex",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": null
},
{
  "id": "core.power-throttling-disable",
  "displayName": "Disable Power Throttling",
  "description": "Stops Windows from throttling background processes' CPU.",
  "category": "power",
  "requiresElevation": true,
  "requiresReboot": false,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling",
  "valueName": "PowerThrottlingOff",
  "valueKind": "DWord",
  "appliedData": "1",
  "riskNote": null
},
{
  "id": "core.modern-standby-disable",
  "displayName": "Disable Modern Standby",
  "description": "Forces classic S3 sleep instead of S0 'Modern Standby'.",
  "category": "power",
  "requiresElevation": true,
  "requiresReboot": true,
  "hive": "LocalMachine",
  "key": "SYSTEM\\CurrentControlSet\\Control\\Power",
  "valueName": "PlatformAoAcOverride",
  "valueKind": "DWord",
  "appliedData": "0",
  "riskNote": "May affect resume-from-sleep behavior on some laptops."
}
```

- [ ] **Step 2: Build, test, smoke test, commit**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json
git commit -m "feat(core): add Power category — 4 registry tweaks"
```

---

## Task 16: Implement `NicPowerManagementTweak` (custom — WMI device prop)

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/NicPowerManagementTweak.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

**Approach:** "Allow the computer to turn off this device to save power" is a per-NIC checkbox. Programmatically it's the `PnPCapabilities` registry value under each NIC's `Class\{4d36e972-...}` subkey, where bit 0x100 means "disable PnP power-down". We set `PnPCapabilities |= 0x100` for every active NIC; revert restores per-NIC backups.

- [ ] **Step 1: Implement (no unit test — depends on real WMI/registry traversal; will be smoke-tested manually)**

Create `src/PrimeOSTuner.Core/Tweaks/NicPowerManagementTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class NicPowerManagementTweak : ITweak
{
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private const int DisablePowerDownBit = 0x100;

    private readonly IRegistryClient _registry;

    public NicPowerManagementTweak(IRegistryClient registry) { _registry = registry; }

    public string Id => "core.nic-power-mgmt";
    public string DisplayName => "Disable NIC power management";
    public string Description => "Stops Windows from turning the network adapter off to save power.";
    public string Category => "network";
    public string? RiskNote => "Slightly higher idle power on laptops.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var subkey in EnumNicSubkeys())
        {
            var v = _registry.ReadDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities") ?? 0;
            if ((v & DisablePowerDownBit) == 0) return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var subkey in EnumNicSubkeys())
        {
            var current = _registry.ReadDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities") ?? 0;
            var newVal = current | DisablePowerDownBit;
            backups.Add(_registry.WriteDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities", newVal));
        }
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backups)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backups = JsonSerializer.Deserialize<List<RegistryBackup>>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backups) _registry.RestoreFromBackup(b);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var nics = EnumNicSubkeys().Count();
        return Task.FromResult($"Will set PnPCapabilities |= 0x100 on {nics} network adapter subkey(s).");
    }

    private static IEnumerable<string> EnumNicSubkeys()
    {
        // Enumerate the four-digit numeric subkeys under the Network Adapter class.
        using var classKey = Registry.LocalMachine.OpenSubKey(ClassRoot);
        if (classKey is null) yield break;
        foreach (var name in classKey.GetSubKeyNames())
        {
            if (name.Length == 4 && int.TryParse(name, out _))
                yield return $"{ClassRoot}\\{name}";
        }
    }
}
```

- [ ] **Step 2: Build & register**

In `App.xaml.cs`: `s.AddSingleton<NicPowerManagementTweak>();` and append to `custom` array.

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

- [ ] **Step 3: Manual smoke test**

Launch the app, navigate to Optimize → Network chip. The "Disable NIC power management" tile should show a ⚠️ risk badge (we add the badge UI in Task 17, so for now just verify the tweak appears, the description text says the risk, and toggling ON/OFF works without exceptions).

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/NicPowerManagementTweak.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(core): add NicPowerManagementTweak"
```

---

## Task 17: Update Optimize tab — chips, category mapping, and risk-flag badge

**Files:**
- Modify: `src/PrimeOSTuner.UI/Views/OptimizeView.xaml`
- Modify: `src/PrimeOSTuner.UI/Views/OptimizeView.xaml.cs`

- [ ] **Step 1: Add 3 new chips to the chip list**

In `OptimizeView.xaml.cs`, find the `_chips.Add(new FilterChipVm(...))` block (constructor, around line 34). Replace it with:

```csharp
_chips.Add(new FilterChipVm("all", "All", true));
_chips.Add(new FilterChipVm("fps", "FPS & Latency"));
_chips.Add(new FilterChipVm("network", "Network"));
_chips.Add(new FilterChipVm("system", "System"));
_chips.Add(new FilterChipVm("privacy", "Privacy"));
_chips.Add(new FilterChipVm("power", "Power"));
```

- [ ] **Step 2: Update `CategoryFor` to use the tweak's own category when available**

The `RegistryTweak`, `ServiceDisableTweak`, `TelemetryDisableTweak`, `CortanaDisableTweak`, `UltimatePerformanceTweak`, `HibernationTweak`, and `NicPowerManagementTweak` all expose a `Category` property. The five existing custom tweaks (`PowerPlan`, `RamCleaner`, `MouseAccel`, `TimerResolution`, `GameMode`, `HwGpuScheduling`, `Nagle`, `CpuCoreParking`, `PerAppGpuPreference`) do not — they get categorized by id-prefix as before.

Add an interface for the optional category:

```csharp
// In src/PrimeOSTuner.Core/Tweaks/ICategorizedTweak.cs (NEW FILE)
namespace PrimeOSTuner.Core.Tweaks;

public interface ICategorizedTweak
{
    string Category { get; }
    string? RiskNote { get; }
}
```

Have `RegistryTweak`, `ServiceDisableTweak`, `TelemetryDisableTweak`, `CortanaDisableTweak`, `UltimatePerformanceTweak`, `HibernationTweak`, and `NicPowerManagementTweak` implement it (they already expose those properties — just add `, ICategorizedTweak` to their class declarations).

In `OptimizeView.xaml.cs`, replace `CategoryFor` with:

```csharp
private static (string Key, string Label) CategoryFor(ITweak tweak)
{
    if (tweak is ICategorizedTweak cat)
    {
        return cat.Category switch
        {
            "fps" => ("fps", "FPS & Latency"),
            "network" => ("network", "Network"),
            "system" => ("system", "System"),
            "privacy" => ("privacy", "Privacy"),
            "power" => ("power", "Power"),
            _ => ("fps", "FPS & Latency")
        };
    }

    // Fallback for legacy tweaks (no Category property).
    if (tweak.Id.StartsWith("game.nagle") || tweak.Id.StartsWith("game.network"))
        return ("network", "Network");
    return ("fps", "FPS & Latency");
}
```

Update the constructor of `TweakRowVm` to take the full tweak rather than just its id:

```csharp
public TweakRowVm(ITweak tweak)
{
    Tweak = tweak;
    var (key, label) = CategoryFor(tweak);
    CategoryKey = key;
    Category = label;
    RiskNote = (tweak as ICategorizedTweak)?.RiskNote;
}

public string? RiskNote { get; }
public bool HasRisk => !string.IsNullOrEmpty(RiskNote);
```

(The existing call site `_allRows = tweaks.Select(t => new TweakRowVm(t)).ToList();` already passes `t` so it's compatible.)

- [ ] **Step 3: Add a risk badge to the row template in `OptimizeView.xaml`**

Open `src/PrimeOSTuner.UI/Views/OptimizeView.xaml`. Find the `DataTemplate` that renders each `TweakRowVm` (it's the template applied to `TweakList`'s `ItemsSource`). Inside the row's header `StackPanel` (next to the display name), add this badge:

```xml
<Border Background="#332ECC71"
        BorderBrush="#FFA01F"
        BorderThickness="1"
        CornerRadius="4"
        Padding="6,2"
        Margin="8,0,0,0"
        Visibility="{Binding HasRisk, Converter={StaticResource BoolToVis}}"
        ToolTip="{Binding RiskNote}">
    <TextBlock Text="⚠ Caution" FontSize="11" FontWeight="SemiBold" Foreground="#FFD580"/>
</Border>
```

If `BoolToVis` is not already defined as a resource, add it to the `UserControl.Resources`:

```xml
<UserControl.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVis"/>
</UserControl.Resources>
```

(If it's already there from prior tasks, leave it.)

- [ ] **Step 4: Build, smoke test**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
dotnet run --project src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

Click each of the 6 chips in turn. Confirm:
- All chip → all tweaks
- FPS & Latency → existing + 2 new (Win32 Priority Separation, Startup Delay) + the existing custom ones (Timer Resolution, Game Mode, etc.)
- Network → Nagle, Network Throttling, 5 new TCP/QoS/NetBT/IPv6 + NIC Power Management (with ⚠ badge)
- System → Mouse Accel, WER, Game DVR, Fullscreen Optimizations, SysMain, WSearch (with badge), DiagTrack
- Privacy → 6 registry + Telemetry + Cortana
- Power → 4 registry (one with badge) + Ultimate Performance + Hibernation + existing PowerPlan

- [ ] **Step 5: Commit**

```powershell
git add src/PrimeOSTuner.Core/Tweaks/ICategorizedTweak.cs src/PrimeOSTuner.Core/Tweaks/RegistryTweak.cs src/PrimeOSTuner.Core/Tweaks/ServiceDisableTweak.cs src/PrimeOSTuner.Core/Tweaks/TelemetryDisableTweak.cs src/PrimeOSTuner.Core/Tweaks/CortanaDisableTweak.cs src/PrimeOSTuner.Core/Tweaks/UltimatePerformanceTweak.cs src/PrimeOSTuner.Core/Tweaks/HibernationTweak.cs src/PrimeOSTuner.Core/Tweaks/NicPowerManagementTweak.cs src/PrimeOSTuner.UI/Views/OptimizeView.xaml src/PrimeOSTuner.UI/Views/OptimizeView.xaml.cs
git commit -m "feat(ui): add System/Privacy/Power filter chips + risk badge

ICategorizedTweak lets new tweaks declare their own category and
optional risk note; OptimizeView shows the badge on tiles that have a
risk note, with the note text as a tooltip."
```

---

## Task 18: Update `BuiltInProfiles` and add a profile-coverage test

**Files:**
- Modify: `src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs`
- Modify: `src/PrimeOSTuner.Tests/Profiles/BuiltInProfilesTests.cs`

- [ ] **Step 1: Read the existing `BuiltInProfiles.cs` to see the current shape**

Open `src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs`. It currently exposes `Basic` and `Performance` `ModeProfile` instances containing lists of tweak ids. Note its existing structure; we'll extend it.

- [ ] **Step 2: Update `Basic` to add a small set of safe tweaks**

Inside `BuiltInProfiles.Basic`, append these ids to its `Tweaks` list (keep all existing entries):

```csharp
"core.win32-priority-separation",
"core.startup-delay",
"core.werror-reporting",
"core.activity-history",
"core.advertising-id",
```

- [ ] **Step 3: Update `Performance` to add the bulk set**

Inside `BuiltInProfiles.Performance`, append (keep existing):

```csharp
"core.win32-priority-separation",
"core.startup-delay",
"core.tcp-ack-frequency",
"core.tcp-delivery-acceleration",
"core.qos-bandwidth",
"core.dns-prefetching",
"core.netbios-disable",
"core.werror-reporting",
"core.game-dvr-disable",
"core.fullscreen-optimizations",
"core.sysmain-disable",
"core.connected-user-experiences",
"core.ceip-disable",
"core.activity-history",
"core.advertising-id",
"core.location-tracking",
"core.feedback-diagnostics",
"core.typing-personalization",
"core.telemetry-disable",
"core.usb-selective-suspend",
"core.power-throttling-disable",
"core.ultimate-performance",
```

- [ ] **Step 4: Add an `Aggressive` profile**

In `BuiltInProfiles.cs` add a new static property below `Performance`:

```csharp
public static ModeProfile Aggressive { get; } = new(
    Id: "aggressive",
    Name: "Aggressive",
    Description: "Performance plus advanced tweaks. Disable IPv6, Cortana, search indexing.",
    Tweaks: new List<string>(Performance.Tweaks)
    {
        "core.ipv6-disable",
        "core.cortana-disable",
        "core.search-indexing-tune",
        "core.modern-standby-disable",
        "core.hibernation-disable",
    });
```

(Adapt to the actual `ModeProfile` constructor — if its parameters are positional, mirror the existing `Basic`/`Performance` literals; if it's a record-with-init, use `with { }`.)

- [ ] **Step 5: Add a test that validates every profile id is registered**

Open `src/PrimeOSTuner.Tests/Profiles/BuiltInProfilesTests.cs`. Add this test (do not delete existing tests):

```csharp
[Fact]
public void All_profile_tweak_ids_resolve_against_the_registered_tweak_set()
{
    // Build the set of known ids that v0.4 ships.
    var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Existing custom tweaks
        "core.power-plan", "core.ram-cleaner", "core.dns-flush",
        "core.windows-update-cache", "core.driver-health",
        "core.driver-store-cleanup", "core.registry-cleanup-safe",
        "game.mouse-accel", "game.timer-resolution", "game.game-mode",
        "game.hw-gpu-scheduling", "game.nagle-algorithm",
        "game.network-throttling", "game.system-responsiveness",
        "game.cpu-core-parking", "game.per-app-gpu-pref",
        // New custom
        "core.telemetry-disable", "core.cortana-disable",
        "core.ultimate-performance", "core.hibernation-disable",
        "core.nic-power-mgmt",
        // Service disables
        "core.sysmain-disable", "core.search-indexing-tune",
        "core.connected-user-experiences",
        // Registry catalog (must match tweaks.json)
        "core.win32-priority-separation", "core.startup-delay",
        "core.tcp-ack-frequency", "core.tcp-delivery-acceleration",
        "core.qos-bandwidth", "core.dns-prefetching",
        "core.netbios-disable", "core.ipv6-disable",
        "core.werror-reporting", "core.game-dvr-disable",
        "core.fullscreen-optimizations",
        "core.ceip-disable", "core.activity-history",
        "core.advertising-id", "core.location-tracking",
        "core.feedback-diagnostics", "core.typing-personalization",
        "core.usb-selective-suspend", "core.pcie-aspm-disable",
        "core.power-throttling-disable", "core.modern-standby-disable",
    };

    foreach (var profile in new[] { BuiltInProfiles.Basic, BuiltInProfiles.Performance, BuiltInProfiles.Aggressive })
    {
        foreach (var id in profile.Tweaks)
        {
            knownIds.Should().Contain(id, because: $"profile '{profile.Id}' references unknown id '{id}'");
        }
    }
}
```

(If `core.dns-prefetching` is not actually in `tweaks.json` — it isn't yet; this plan didn't add it — remove it from the `knownIds` set AND from the Performance profile in Step 3. Or, if you're adding it, append it to `tweaks.json` now with: hive `LocalMachine`, key `SYSTEM\CurrentControlSet\Services\Dnscache\Parameters`, valueName `DisableParallelAandAAAA`, kind `DWord`, applied `1`. **Decision for this plan: drop `core.dns-prefetching` from both places — keep this plan's catalog tight.**)

After the decision: remove `"core.dns-prefetching"` from both Performance's `Tweaks` list (Step 3) and from `knownIds`.

- [ ] **Step 6: Run the test, confirm pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter "FullyQualifiedName~BuiltInProfilesTests"
```

Expected: pass. If it fails because `Aggressive` isn't wired into the lifecycle's profile dictionary, that's fine — the test only checks ids resolve.

- [ ] **Step 7: Wire `Aggressive` into `ProfileLifecycleService`**

In `App.xaml.cs`, find the `ProfileLifecycleService` registration (around line 108-124) where the `dict` of profiles is built. Add this line inside the dictionary initializer:

```csharp
["aggressive"] = BuiltInProfiles.Aggressive,
```

- [ ] **Step 8: Build, smoke test, commit**

```powershell
dotnet build src/PrimeOSTuner.sln
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git add src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs src/PrimeOSTuner.Tests/Profiles/BuiltInProfilesTests.cs src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(profiles): expand Basic + Performance, add Aggressive profile"
```

---

## Task 19: Final integration smoke test + tag

- [ ] **Step 1: Manual end-to-end test**

Launch the app:

```powershell
dotnet run --project src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

For each chip in Optimize, verify:
- Chip click filters list to that category only
- ⚠ Caution badge appears on the right tiles (IPv6, NIC Power, WSearch, Modern Standby) and tooltip shows the risk note
- Toggle a couple of tweaks ON, then OFF — no exceptions
- Hit "OPTIMIZE NOW" on the Dashboard — runs without errors and the boost score recalculates

- [ ] **Step 2: Run the full test suite**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Expected: all green.

- [ ] **Step 3: Commit any final fixes (if needed) and tag**

```powershell
git tag -a v0.4a -m "v0.4a: Optimizer Pack — RegistryTweak + ~30 tweaks across 5 categories"
```

(Don't push yet — v0.4 ships when 4b and 4c are done too.)

- [ ] **Step 4: Publish a fresh build to `publish/v0.4a/`**

```powershell
dotnet publish src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj -c Release -r win-x64 --self-contained false -o publish/v0.4a/
```

Open the publish folder, confirm `Tweaks\catalog\tweaks.json` is present next to the .exe. Run the published exe, confirm it launches and the new tweaks show up in Optimize.

---

## Self-Review Notes

- **Spec coverage check:**
  - §4.1 FPS & Latency: 6 existing + 2 new (Win32PrioritySeparation, StartupDelay) ✅ Tasks 5, 17
  - §4.2 Network: 2 existing migrated + 5 new + NIC custom ✅ Tasks 6, 7, 16
  - §4.3 System: 1 existing + 3 registry new + 3 service new ✅ Task 9
  - §4.4 Privacy: 6 registry + Telemetry + Cortana ✅ Tasks 10, 11, 12
  - §4.5 Power: 4 registry + UltimatePerformance + Hibernation ✅ Tasks 13, 14, 15
  - §4.6 Risk-flagged tweaks: ⚠ badge + tooltip ✅ Task 17
  - §3.4 Filter chips (6 total) ✅ Task 17
  - §7 Built-in Profiles updated ✅ Task 18

- **Out of scope (deferred to v0.4b / v0.4c):**
  - Bloatware tab → v0.4b
  - Memory Priority tab → v0.4c
  - `core.dns-prefetching` was in spec §4.2 but dropped from this plan to keep the catalog tight; can be added with a one-line JSON append later.

- **Type consistency:** Method names and properties used consistently (`ProbeAsync`, `ApplyAsync`, `RevertAsync`, `RestoreFromBackup`, `RunPowercfg`, `Read`/`SetStartType`/`Stop` on `IServiceClient`).

- **No placeholders:** every step has the actual code, exact paths, and exact commands.
