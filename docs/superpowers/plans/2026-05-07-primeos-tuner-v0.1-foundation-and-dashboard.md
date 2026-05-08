# PrimeOS Tuner v0.1 — Foundation + Dashboard + Optimize Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v0.1 of PrimeOS Tuner — a working, installable Windows desktop app with a Hone-style dark dashboard, live system monitoring, a tweak-history undo log, and a one-click optimize button that runs four safe tweaks (junk file cleanup, power plan switch, RAM trim, visual effects).

**Architecture:** WPF app with a 3-layer split (UI → Core → Win) plus a unit-test project. Every optimization implements a common `ITweak` interface so the pipeline, history, and UI can treat them uniformly. The Win layer isolates all OS calls behind interfaces so Core logic can be unit-tested with mocks.

**Tech Stack:** .NET 8, C# 12, WPF, WPF-UI (Fluent controls), CommunityToolkit.Mvvm, LiveCharts2, LibreHardwareMonitorLib, Serilog, xUnit, Moq.

---

## Working with this plan

This plan is built for a beginner. Every task lists exact commands, exact code, and what to expect. Work top-to-bottom, one task at a time. Don't skip ahead — later tasks assume earlier ones are committed.

- **Check off** the box on every step as you finish it (in your editor, replace `[ ]` with `[x]`).
- **Run the exact command shown.** If output differs from the "Expected" line, stop and ask before continuing.
- **Commit at the end of every task.** Many small commits are better than a few big ones — they're your undo button.
- **TDD rule** (for Core/Win/ViewModel tasks): write the test, watch it fail, write code, watch it pass, commit. Resist the urge to write code first.
- **VM rule**: any task that hits the registry, services, or system files must be run inside the Windows 11 VM, never on your real PC, until v0.1 is complete and smoke-tested.

---

## Prerequisites (one-time machine setup)

Do these once before Task 1.

- [ ] **Install .NET 8 SDK**
  - Download: <https://dotnet.microsoft.com/en-us/download/dotnet/8.0> → "SDK 8.0.x" → "Windows x64 Installer"
  - Verify: `dotnet --version` in PowerShell should print `8.0.x`

- [ ] **Install Visual Studio 2022 Community** (free)
  - Download: <https://visualstudio.microsoft.com/vs/community/>
  - In the installer, **check the "**.NET desktop development**" workload**. Don't check anything else for now.

- [ ] **Install Git for Windows** (if not already)
  - Download: <https://git-scm.com/download/win>
  - Verify: `git --version` should print git version info

- [ ] **Set up a Windows 11 VM** for testing
  - Easiest path: install **VirtualBox** (<https://www.virtualbox.org/>), download the **free Windows 11 Dev VM** from Microsoft (<https://developer.microsoft.com/en-us/windows/downloads/virtual-machines/>)
  - Inside the VM: enable system restore (`SystemPropertiesProtection.exe` → enable on `C:`)
  - **Take a snapshot** in VirtualBox named `clean-baseline` — you will restore to this between tests

- [ ] **Confirm working directory**
  - In PowerShell: `cd "C:\Users\jaxso\projects\PC Performance booster"` and `git status` should show a clean main branch with the spec already committed.

---

## File structure (what we're building)

```
PrimeOS Tuner/
├── docs/superpowers/
│   ├── specs/2026-05-07-primeos-tuner-design.md       (already exists)
│   └── plans/2026-05-07-...-foundation-and-dashboard.md (this file)
├── src/
│   ├── PrimeOSTuner.UI/         WPF executable
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Theme/
│   │   │   ├── Colors.xaml
│   │   │   └── Styles.xaml
│   │   ├── Views/
│   │   │   ├── DashboardView.xaml
│   │   │   ├── OptimizeView.xaml
│   │   │   └── HistoryView.xaml
│   │   ├── ViewModels/
│   │   │   ├── ShellViewModel.cs
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── OptimizeViewModel.cs
│   │   │   └── HistoryViewModel.cs
│   │   └── Controls/
│   │       ├── BoostScoreRing.xaml
│   │       └── StatCard.xaml
│   ├── PrimeOSTuner.Core/       Pure C# library — no Win API
│   │   ├── Tweaks/
│   │   │   ├── ITweak.cs
│   │   │   ├── TweakResult.cs
│   │   │   ├── TweakState.cs
│   │   │   ├── JunkFileTweak.cs
│   │   │   ├── PowerPlanTweak.cs
│   │   │   ├── RamCleanerTweak.cs
│   │   │   └── VisualEffectsTweak.cs
│   │   ├── History/
│   │   │   ├── HistoryEntry.cs
│   │   │   └── TweakHistory.cs
│   │   ├── Monitoring/
│   │   │   ├── SystemSample.cs
│   │   │   ├── SystemSampler.cs
│   │   │   └── BoostScoreCalculator.cs
│   │   └── Pipeline/
│   │       └── OneClickOptimizer.cs
│   ├── PrimeOSTuner.Win/        Thin OS wrappers (the "scary" layer)
│   │   ├── IRegistryClient.cs
│   │   ├── RegistryClient.cs
│   │   ├── IProcessClient.cs
│   │   ├── ProcessClient.cs
│   │   ├── IRestorePointClient.cs
│   │   ├── RestorePointClient.cs
│   │   ├── IHardwareClient.cs
│   │   ├── HardwareClient.cs
│   │   ├── IPowerPlanClient.cs
│   │   ├── PowerPlanClient.cs
│   │   └── PInvoke.cs
│   └── PrimeOSTuner.Tests/      xUnit tests
│       ├── Tweaks/
│       ├── History/
│       ├── Monitoring/
│       └── Pipeline/
└── PrimeOSTuner.sln
```

Each file has one clear job. Tests live in a separate project so they don't ship in the release binary.

---

## Phase A — Solution & Project Skeleton

### Task 1: Create the solution and four projects

**Files:**
- Create: `PrimeOSTuner.sln`
- Create: `src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj`
- Create: `src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj`
- Create: `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj`
- Create: `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj`

- [ ] **Step 1: Create the solution file at the repo root**

```powershell
dotnet new sln -n PrimeOSTuner
```

Expected: `The template "Solution File" was created successfully.`

- [ ] **Step 2: Create the Core class library**

```powershell
dotnet new classlib -n PrimeOSTuner.Core -o src/PrimeOSTuner.Core -f net8.0
```

Expected: `The template "Class Library" was created successfully.`

- [ ] **Step 3: Create the Win class library**

```powershell
dotnet new classlib -n PrimeOSTuner.Win -o src/PrimeOSTuner.Win -f net8.0-windows
```

Note: `net8.0-windows` (not `net8.0`) — this layer needs Windows-specific APIs.

- [ ] **Step 4: Create the WPF UI executable**

```powershell
dotnet new wpf -n PrimeOSTuner.UI -o src/PrimeOSTuner.UI -f net8.0-windows
```

- [ ] **Step 5: Create the xUnit test project**

```powershell
dotnet new xunit -n PrimeOSTuner.Tests -o src/PrimeOSTuner.Tests -f net8.0-windows
```

- [ ] **Step 6: Add all four projects to the solution**

```powershell
dotnet sln add src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

- [ ] **Step 7: Wire up project references**

```powershell
dotnet add src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj reference src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj
dotnet add src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj reference src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj
dotnet add src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj reference src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj
```

- [ ] **Step 8: Build to verify the skeleton compiles**

```powershell
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 9: Commit**

```powershell
git add .
git commit -m "Add empty solution skeleton with 4 projects"
```

---

### Task 2: Install NuGet packages

**Files:** all four `.csproj` files get package additions.

- [ ] **Step 1: UI packages (WPF-UI, MVVM toolkit, charts, hosting, Serilog)**

```powershell
dotnet add src/PrimeOSTuner.UI package WPF-UI --version 3.0.5
dotnet add src/PrimeOSTuner.UI package CommunityToolkit.Mvvm --version 8.4.0
dotnet add src/PrimeOSTuner.UI package LiveChartsCore.SkiaSharpView.WPF --version 2.0.0-rc5.4
dotnet add src/PrimeOSTuner.UI package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add src/PrimeOSTuner.UI package Serilog.Extensions.Hosting --version 8.0.0
dotnet add src/PrimeOSTuner.UI package Serilog.Sinks.File --version 6.0.0
dotnet add src/PrimeOSTuner.UI package Serilog.Sinks.Console --version 6.0.0
```

- [ ] **Step 2: Core packages (MVVM toolkit for ObservableObject, JSON)**

```powershell
dotnet add src/PrimeOSTuner.Core package CommunityToolkit.Mvvm --version 8.4.0
dotnet add src/PrimeOSTuner.Core package System.Text.Json --version 8.0.5
```

- [ ] **Step 3: Win packages (LibreHardwareMonitor, Registry)**

```powershell
dotnet add src/PrimeOSTuner.Win package LibreHardwareMonitorLib --version 0.9.4
dotnet add src/PrimeOSTuner.Win package Microsoft.Win32.Registry --version 5.0.0
dotnet add src/PrimeOSTuner.Win package System.Management --version 8.0.0
```

- [ ] **Step 4: Test packages (Moq for mocking, FluentAssertions for nicer asserts)**

```powershell
dotnet add src/PrimeOSTuner.Tests package Moq --version 4.20.72
dotnet add src/PrimeOSTuner.Tests package FluentAssertions --version 6.12.2
```

- [ ] **Step 5: Build to confirm everything restores cleanly**

```powershell
dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add NuGet packages to all projects"
```

---

### Task 3: Smoke test — empty WPF window runs

**Files:**
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml`

- [ ] **Step 1: Replace `MainWindow.xaml` content** with a minimal placeholder

```xml
<Window x:Class="PrimeOSTuner.UI.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PrimeOS Tuner — Skeleton"
        Height="600" Width="900"
        Background="#06080c">
    <Grid>
        <TextBlock Text="PrimeOS Tuner skeleton boot OK"
                   Foreground="#00e5c5"
                   FontSize="24"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"/>
    </Grid>
</Window>
```

- [ ] **Step 2: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: A near-black window opens with cyan text "PrimeOS Tuner skeleton boot OK". Close it.

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Smoke test: empty WPF window boots"
```

---

## Phase B — Core Domain Types

### Task 4: TweakResult and TweakState

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/TweakState.cs`
- Create: `src/PrimeOSTuner.Core/Tweaks/TweakResult.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/TweakResultTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/TweakResultTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TweakResultTests
{
    [Fact]
    public void Success_factory_returns_succeeded_result_with_undo_data()
    {
        var result = TweakResult.Success("undo-payload");

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Be("undo-payload");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_factory_returns_failed_result_with_error_message()
    {
        var result = TweakResult.Failure("boom");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("boom");
        result.UndoData.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run the test, watch it fail**

```powershell
dotnet test --filter FullyQualifiedName~TweakResultTests
```

Expected: build error (`TweakResult` doesn't exist).

- [ ] **Step 3: Implement `TweakState.cs`**

```csharp
namespace PrimeOSTuner.Core.Tweaks;

public enum TweakState
{
    Unknown,
    NotApplied,
    Applied,
    PartiallyApplied
}
```

- [ ] **Step 4: Implement `TweakResult.cs`**

```csharp
namespace PrimeOSTuner.Core.Tweaks;

public sealed record TweakResult(bool Succeeded, string? UndoData, string? Error)
{
    public static TweakResult Success(string? undoData = null) => new(true, undoData, null);
    public static TweakResult Failure(string error) => new(false, null, error);
}
```

- [ ] **Step 5: Run the test, watch it pass**

```powershell
dotnet test --filter FullyQualifiedName~TweakResultTests
```

Expected: `Passed! - 2 passed, 0 failed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add TweakResult and TweakState core types with tests"
```

---

### Task 5: ITweak interface

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/ITweak.cs`

- [ ] **Step 1: No test needed** — this is a pure interface contract. Skip TDD; create the file.

`src/PrimeOSTuner.Core/Tweaks/ITweak.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace PrimeOSTuner.Core.Tweaks;

public interface ITweak
{
    string Id { get; }                 // stable identifier, e.g. "core.junk-files"
    string DisplayName { get; }        // shown in UI
    string Description { get; }        // plain-language explanation
    bool RequiresElevation { get; }    // does Apply need admin?
    bool IsDestructive { get; }        // requires manual opt-in (never auto-run)

    Task<TweakState> ProbeAsync(CancellationToken ct = default);
    Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default);
    Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default);
    Task<string> PreviewAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Build to confirm the interface compiles**

```powershell
dotnet build src/PrimeOSTuner.Core
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add ITweak interface defining the tweak contract"
```

---

## Phase C — History

### Task 6: HistoryEntry record + JSON round-trip test

**Files:**
- Create: `src/PrimeOSTuner.Core/History/HistoryEntry.cs`
- Create: `src/PrimeOSTuner.Tests/History/HistoryEntryTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/History/HistoryEntryTests.cs`:

```csharp
using System;
using System.Text.Json;
using FluentAssertions;
using PrimeOSTuner.Core.History;
using Xunit;

namespace PrimeOSTuner.Tests.History;

public class HistoryEntryTests
{
    [Fact]
    public void HistoryEntry_round_trips_through_json()
    {
        var entry = new HistoryEntry(
            Id: Guid.NewGuid(),
            TweakId: "core.junk-files",
            DisplayName: "Junk file cleanup",
            AppliedAtUtc: DateTime.UtcNow,
            UndoData: "{\"freed\":2048}",
            Reverted: false);

        var json = JsonSerializer.Serialize(entry);
        var round = JsonSerializer.Deserialize<HistoryEntry>(json);

        round.Should().NotBeNull();
        round!.Should().BeEquivalentTo(entry);
    }
}
```

- [ ] **Step 2: Run, watch it fail**

```powershell
dotnet test --filter FullyQualifiedName~HistoryEntryTests
```

Expected: build error (`HistoryEntry` undefined).

- [ ] **Step 3: Implement `HistoryEntry.cs`**

```csharp
using System;

namespace PrimeOSTuner.Core.History;

public sealed record HistoryEntry(
    Guid Id,
    string TweakId,
    string DisplayName,
    DateTime AppliedAtUtc,
    string? UndoData,
    bool Reverted);
```

- [ ] **Step 4: Run, watch it pass**

```powershell
dotnet test --filter FullyQualifiedName~HistoryEntryTests
```

Expected: `Passed! - 1 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add HistoryEntry record with JSON round-trip test"
```

---

### Task 7: TweakHistory append + load

**Files:**
- Create: `src/PrimeOSTuner.Core/History/TweakHistory.cs`
- Create: `src/PrimeOSTuner.Tests/History/TweakHistoryTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/PrimeOSTuner.Tests/History/TweakHistoryTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PrimeOSTuner.Core.History;
using Xunit;

namespace PrimeOSTuner.Tests.History;

public class TweakHistoryTests : IDisposable
{
    private readonly string _tempPath;

    public TweakHistoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"primeos-history-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public async Task Append_creates_file_when_missing()
    {
        var history = new TweakHistory(_tempPath);

        await history.AppendAsync(new HistoryEntry(
            Guid.NewGuid(), "x", "X", DateTime.UtcNow, null, false));

        File.Exists(_tempPath).Should().BeTrue();
    }

    [Fact]
    public async Task Load_returns_entries_in_append_order()
    {
        var history = new TweakHistory(_tempPath);
        var first = new HistoryEntry(Guid.NewGuid(), "a", "A", DateTime.UtcNow, null, false);
        var second = new HistoryEntry(Guid.NewGuid(), "b", "B", DateTime.UtcNow.AddSeconds(1), null, false);

        await history.AppendAsync(first);
        await history.AppendAsync(second);

        var entries = (await history.LoadAsync()).ToList();
        entries.Should().HaveCount(2);
        entries[0].TweakId.Should().Be("a");
        entries[1].TweakId.Should().Be("b");
    }

    [Fact]
    public async Task MarkReverted_updates_entry_in_place()
    {
        var history = new TweakHistory(_tempPath);
        var entry = new HistoryEntry(Guid.NewGuid(), "a", "A", DateTime.UtcNow, "u", false);
        await history.AppendAsync(entry);

        await history.MarkRevertedAsync(entry.Id);

        var loaded = (await history.LoadAsync()).Single();
        loaded.Reverted.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run, watch them fail**

```powershell
dotnet test --filter FullyQualifiedName~TweakHistoryTests
```

Expected: build error (`TweakHistory` undefined).

- [ ] **Step 3: Implement `TweakHistory.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PrimeOSTuner.Core.History;

public sealed class TweakHistory
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public TweakHistory(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "history.json");

    public async Task AppendAsync(HistoryEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            entries.Add(entry);
            await SaveAsync(entries);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync()
    {
        await _lock.WaitAsync();
        try { return await LoadInternalAsync(); }
        finally { _lock.Release(); }
    }

    public async Task MarkRevertedAsync(Guid entryId)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == entryId)
                {
                    entries[i] = entries[i] with { Reverted = true };
                }
            }
            await SaveAsync(entries);
        }
        finally { _lock.Release(); }
    }

    private async Task<List<HistoryEntry>> LoadInternalAsync()
    {
        if (!File.Exists(_filePath)) return new List<HistoryEntry>();
        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<HistoryEntry>();
        return JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts) ?? new List<HistoryEntry>();
    }

    private async Task SaveAsync(List<HistoryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
```

- [ ] **Step 4: Run, watch them pass**

```powershell
dotnet test --filter FullyQualifiedName~TweakHistoryTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add TweakHistory append/load/revert with file-backed JSON persistence"
```

---

## Phase D — Win Layer

These tasks introduce the OS wrappers. We use interfaces so Core can be unit-tested with Moq.

### Task 8: IRegistryClient + RegistryClient with backup-on-write

**Files:**
- Create: `src/PrimeOSTuner.Win/IRegistryClient.cs`
- Create: `src/PrimeOSTuner.Win/RegistryClient.cs`
- Create: `src/PrimeOSTuner.Tests/Win/RegistryClientTests.cs`

- [ ] **Step 1: Write the failing integration test** (uses real registry under HKCU\Software\PrimeOSTuner\TestArea — safe because HKCU is per-user and we clean up)

`src/PrimeOSTuner.Tests/Win/RegistryClientTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run, watch them fail**

```powershell
dotnet test --filter FullyQualifiedName~RegistryClientTests
```

Expected: build error (`RegistryClient` undefined).

- [ ] **Step 3: Implement the interface**

`src/PrimeOSTuner.Win/IRegistryClient.cs`:

```csharp
using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed record RegistryBackup(RegistryHive Hive, string SubKey, string ValueName, string? PreviousValue);

public interface IRegistryClient
{
    string? ReadString(RegistryHive hive, string subKey, string valueName);
    RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue);
    void RestoreFromBackup(RegistryBackup backup);
}
```

- [ ] **Step 4: Implement `RegistryClient.cs`**

```csharp
using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed class RegistryClient : IRegistryClient
{
    public string? ReadString(RegistryHive hive, string subKey, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey);
        return key?.GetValue(valueName) as string;
    }

    public RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue)
    {
        var previous = ReadString(hive, subKey, valueName);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Cannot open or create {subKey}");
        key.SetValue(valueName, newValue, RegistryValueKind.String);
        return new RegistryBackup(hive, subKey, valueName, previous);
    }

    public void RestoreFromBackup(RegistryBackup backup)
    {
        using var baseKey = RegistryKey.OpenBaseKey(backup.Hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(backup.SubKey, writable: true);
        if (key is null) return;

        if (backup.PreviousValue is null)
            key.DeleteValue(backup.ValueName, throwOnMissingValue: false);
        else
            key.SetValue(backup.ValueName, backup.PreviousValue, RegistryValueKind.String);
    }
}
```

- [ ] **Step 5: Run, watch them pass**

```powershell
dotnet test --filter FullyQualifiedName~RegistryClientTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add RegistryClient with backup/restore-on-write semantics"
```

---

### Task 9: IProcessClient + ProcessClient (RAM trim via EmptyWorkingSet)

**Files:**
- Create: `src/PrimeOSTuner.Win/IProcessClient.cs`
- Create: `src/PrimeOSTuner.Win/ProcessClient.cs`
- Create: `src/PrimeOSTuner.Win/PInvoke.cs`
- Create: `src/PrimeOSTuner.Tests/Win/ProcessClientTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Win/ProcessClientTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run, watch them fail**

```powershell
dotnet test --filter FullyQualifiedName~ProcessClientTests
```

Expected: build error.

- [ ] **Step 3: Implement P/Invoke and interface**

`src/PrimeOSTuner.Win/PInvoke.cs`:

```csharp
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

internal static class PInvoke
{
    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("ntdll.dll")]
    public static extern int NtSetTimerResolution(uint desiredResolution100ns, bool setResolution, out uint currentResolution);
}
```

`src/PrimeOSTuner.Win/IProcessClient.cs`:

```csharp
namespace PrimeOSTuner.Win;

public interface IProcessClient
{
    void TrimWorkingSet(int processId);
    int TrimAllUserProcesses();
}
```

- [ ] **Step 4: Implement `ProcessClient.cs`**

```csharp
using System.Diagnostics;

namespace PrimeOSTuner.Win;

public sealed class ProcessClient : IProcessClient
{
    public void TrimWorkingSet(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            PInvoke.EmptyWorkingSet(p.Handle);
        }
        catch (ArgumentException) { /* process exited between enumerate and trim */ }
        catch (InvalidOperationException) { /* same */ }
    }

    public int TrimAllUserProcesses()
    {
        var attempted = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                PInvoke.EmptyWorkingSet(p.Handle);
                attempted++;
            }
            catch { /* protected processes will refuse — that's expected */ }
            finally { p.Dispose(); }
        }
        return attempted;
    }
}
```

- [ ] **Step 5: Run, watch them pass**

```powershell
dotnet test --filter FullyQualifiedName~ProcessClientTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add ProcessClient with EmptyWorkingSet for RAM trim"
```

---

### Task 10: IPowerPlanClient + PowerPlanClient

**Files:**
- Create: `src/PrimeOSTuner.Win/IPowerPlanClient.cs`
- Create: `src/PrimeOSTuner.Win/PowerPlanClient.cs`
- Create: `src/PrimeOSTuner.Tests/Win/PowerPlanClientTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Win/PowerPlanClientTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class PowerPlanClientTests
{
    [Fact]
    public void ListPlans_includes_at_least_balanced()
    {
        var client = new PowerPlanClient();
        var plans = client.ListPlans();

        plans.Should().Contain(p => p.Name.Equals("Balanced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetActivePlan_returns_a_plan_from_the_listed_set()
    {
        var client = new PowerPlanClient();
        var plans = client.ListPlans();
        var active = client.GetActivePlan();

        plans.Should().Contain(p => p.Guid == active.Guid);
    }
}
```

- [ ] **Step 2: Run, watch them fail**

```powershell
dotnet test --filter FullyQualifiedName~PowerPlanClientTests
```

Expected: build error.

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Win/IPowerPlanClient.cs`:

```csharp
namespace PrimeOSTuner.Win;

public sealed record PowerPlan(Guid Guid, string Name);

public interface IPowerPlanClient
{
    IReadOnlyList<PowerPlan> ListPlans();
    PowerPlan GetActivePlan();
    void SetActivePlan(Guid planGuid);
    Guid EnsureUltimatePerformancePlan();
}
```

- [ ] **Step 4: Implement `PowerPlanClient.cs`** (uses `powercfg.exe` via Process)

```csharp
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PrimeOSTuner.Win;

public sealed class PowerPlanClient : IPowerPlanClient
{
    private static readonly Guid UltimatePerformanceTemplate =
        new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    public IReadOnlyList<PowerPlan> ListPlans()
    {
        var output = RunPowerCfg("/list");
        var plans = new List<PowerPlan>();
        var rx = new Regex(@"GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)");
        foreach (Match m in rx.Matches(output))
        {
            plans.Add(new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim()));
        }
        return plans;
    }

    public PowerPlan GetActivePlan()
    {
        var output = RunPowerCfg("/getactivescheme");
        var rx = new Regex(@"GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)");
        var m = rx.Match(output);
        if (!m.Success) throw new InvalidOperationException($"Could not parse: {output}");
        return new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim());
    }

    public void SetActivePlan(Guid planGuid) => RunPowerCfg($"/setactive {planGuid:D}");

    public Guid EnsureUltimatePerformancePlan()
    {
        var existing = ListPlans().FirstOrDefault(p =>
            p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Guid;

        var output = RunPowerCfg($"/duplicatescheme {UltimatePerformanceTemplate:D}");
        var rx = new Regex(@"([0-9a-fA-F-]{36})");
        var m = rx.Match(output);
        if (!m.Success) throw new InvalidOperationException($"Could not duplicate ultimate plan: {output}");
        return Guid.Parse(m.Value);
    }

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && string.IsNullOrEmpty(output))
            throw new InvalidOperationException($"powercfg failed: {error}");
        return output;
    }
}
```

- [ ] **Step 5: Run, watch them pass**

```powershell
dotnet test --filter FullyQualifiedName~PowerPlanClientTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add PowerPlanClient wrapping powercfg.exe"
```

---

### Task 11: IRestorePointClient + RestorePointClient (WMI)

**Files:**
- Create: `src/PrimeOSTuner.Win/IRestorePointClient.cs`
- Create: `src/PrimeOSTuner.Win/RestorePointClient.cs`
- Create: `src/PrimeOSTuner.Tests/Win/RestorePointClientTests.cs`

- [ ] **Step 1: Write the failing test** (note: Create requires admin and System Restore enabled — skipped in CI but verified manually)

`src/PrimeOSTuner.Tests/Win/RestorePointClientTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~RestorePointClientTests
```

Expected: build error.

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Win/IRestorePointClient.cs`:

```csharp
namespace PrimeOSTuner.Win;

public interface IRestorePointClient
{
    bool IsAvailable();
    bool TryCreate(string description, out string? error);
}
```

- [ ] **Step 4: Implement `RestorePointClient.cs`** using WMI

```csharp
using System.Management;

namespace PrimeOSTuner.Win;

public sealed class RestorePointClient : IRestorePointClient
{
    public bool IsAvailable()
    {
        try
        {
            using var scope = new ManagementScope(@"\\.\root\default");
            scope.Connect();
            return scope.IsConnected;
        }
        catch { return false; }
    }

    public bool TryCreate(string description, out string? error)
    {
        error = null;
        try
        {
            var path = new ManagementPath(@"\\.\root\default:SystemRestore");
            using var sysRestore = new ManagementClass(path);
            var args = sysRestore.GetMethodParameters("CreateRestorePoint");
            args["Description"] = description;
            args["RestorePointType"] = 12; // MODIFY_SETTINGS
            args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
            var result = sysRestore.InvokeMethod("CreateRestorePoint", args, null);
            var rc = Convert.ToInt32(result["ReturnValue"]);
            if (rc == 0) return true;
            error = $"CreateRestorePoint returned {rc}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~RestorePointClientTests
```

Expected: `Passed! - 1 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add RestorePointClient using WMI SystemRestore"
```

---

### Task 12: IHardwareClient + HardwareClient (CPU/RAM/network sampling)

**Files:**
- Create: `src/PrimeOSTuner.Win/IHardwareClient.cs`
- Create: `src/PrimeOSTuner.Win/HardwareClient.cs`
- Create: `src/PrimeOSTuner.Tests/Win/HardwareClientTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Win/HardwareClientTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class HardwareClientTests
{
    [Fact]
    public void Snapshot_returns_plausible_values()
    {
        using var client = new HardwareClient();
        var snap = client.Snapshot();

        snap.CpuPercent.Should().BeInRange(0, 100);
        snap.RamUsedBytes.Should().BeGreaterThan(0);
        snap.RamTotalBytes.Should().BeGreaterThan(snap.RamUsedBytes);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~HardwareClientTests
```

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Win/IHardwareClient.cs`:

```csharp
namespace PrimeOSTuner.Win;

public sealed record HardwareSnapshot(
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    double GpuPercent,
    double GpuTempC,
    long NetworkDownBps,
    long NetworkUpBps);

public interface IHardwareClient : IDisposable
{
    HardwareSnapshot Snapshot();
}
```

- [ ] **Step 4: Implement `HardwareClient.cs`** wrapping LibreHardwareMonitor

```csharp
using LibreHardwareMonitor.Hardware;

namespace PrimeOSTuner.Win;

public sealed class HardwareClient : IHardwareClient
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsNetworkEnabled = true
    };

    public HardwareClient() => _computer.Open();

    public HardwareSnapshot Snapshot()
    {
        foreach (var hw in _computer.Hardware) hw.Update();

        var cpu = ReadFirst(HardwareType.Cpu, SensorType.Load, "CPU Total") ?? 0;
        var ramUsed = (ReadFirst(HardwareType.Memory, SensorType.Data, "Memory Used") ?? 0) * 1024L * 1024L * 1024L;
        var ramAvail = (ReadFirst(HardwareType.Memory, SensorType.Data, "Memory Available") ?? 0) * 1024L * 1024L * 1024L;
        var ramTotal = (long)(ramUsed + ramAvail);
        var gpuLoad = ReadAny(new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel }, SensorType.Load, "GPU Core") ?? 0;
        var gpuTemp = ReadAny(new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel }, SensorType.Temperature, "GPU Core") ?? 0;
        var netDown = (long)(ReadFirst(HardwareType.Network, SensorType.Throughput, "Download Speed") ?? 0);
        var netUp = (long)(ReadFirst(HardwareType.Network, SensorType.Throughput, "Upload Speed") ?? 0);

        return new HardwareSnapshot(cpu, (long)ramUsed, ramTotal, gpuLoad, gpuTemp, netDown, netUp);
    }

    private float? ReadFirst(HardwareType hwType, SensorType sensorType, string name)
    {
        foreach (var hw in _computer.Hardware)
            if (hw.HardwareType == hwType)
                foreach (var s in hw.Sensors)
                    if (s.SensorType == sensorType && s.Name == name) return s.Value;
        return null;
    }

    private float? ReadAny(HardwareType[] types, SensorType sensorType, string name)
    {
        foreach (var t in types)
        {
            var v = ReadFirst(t, sensorType, name);
            if (v.HasValue) return v;
        }
        return null;
    }

    public void Dispose() => _computer.Close();
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~HardwareClientTests
```

Expected: `Passed! - 1 passed`. (If GPU sensors are missing in your VM, GpuPercent and GpuTempC may be 0 — that's fine for the test.)

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add HardwareClient wrapping LibreHardwareMonitor"
```

---

## Phase E — Monitoring & Scoring

### Task 13: BoostScoreCalculator (pure logic, full TDD)

**Files:**
- Create: `src/PrimeOSTuner.Core/Monitoring/BoostScoreCalculator.cs`
- Create: `src/PrimeOSTuner.Core/Monitoring/BoostScoreInputs.cs`
- Create: `src/PrimeOSTuner.Tests/Monitoring/BoostScoreCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/PrimeOSTuner.Tests/Monitoring/BoostScoreCalculatorTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Monitoring;
using Xunit;

namespace PrimeOSTuner.Tests.Monitoring;

public class BoostScoreCalculatorTests
{
    [Fact]
    public void Score_for_a_pristine_pc_is_one_hundred()
    {
        var inputs = new BoostScoreInputs(
            JunkBytes: 0,
            HighPerformancePower: true,
            VisualEffectsOptimized: true,
            MouseAccelDisabled: true,
            TelemetryDisabled: true,
            BloatwareCount: 0);

        BoostScoreCalculator.Compute(inputs).Should().Be(100);
    }

    [Fact]
    public void Score_for_a_fresh_windows_is_low()
    {
        var inputs = new BoostScoreInputs(
            JunkBytes: 5L * 1024 * 1024 * 1024, // 5 GB
            HighPerformancePower: false,
            VisualEffectsOptimized: false,
            MouseAccelDisabled: false,
            TelemetryDisabled: false,
            BloatwareCount: 12);

        BoostScoreCalculator.Compute(inputs).Should().BeLessThan(40);
    }

    [Fact]
    public void Score_is_clamped_to_zero_one_hundred()
    {
        var ridiculous = new BoostScoreInputs(
            JunkBytes: long.MaxValue,
            HighPerformancePower: false,
            VisualEffectsOptimized: false,
            MouseAccelDisabled: false,
            TelemetryDisabled: false,
            BloatwareCount: 1000);

        var s = BoostScoreCalculator.Compute(ridiculous);
        s.Should().BeInRange(0, 100);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~BoostScoreCalculatorTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Monitoring/BoostScoreInputs.cs`:

```csharp
namespace PrimeOSTuner.Core.Monitoring;

public sealed record BoostScoreInputs(
    long JunkBytes,
    bool HighPerformancePower,
    bool VisualEffectsOptimized,
    bool MouseAccelDisabled,
    bool TelemetryDisabled,
    int BloatwareCount);
```

`src/PrimeOSTuner.Core/Monitoring/BoostScoreCalculator.cs`:

```csharp
namespace PrimeOSTuner.Core.Monitoring;

public static class BoostScoreCalculator
{
    public static int Compute(BoostScoreInputs i)
    {
        // Start at 100 and subtract penalties.
        var score = 100.0;
        score -= Math.Min(25, i.JunkBytes / (double)(1L << 30) * 5); // up to -25 for >5GB junk
        if (!i.HighPerformancePower)      score -= 10;
        if (!i.VisualEffectsOptimized)    score -= 5;
        if (!i.MouseAccelDisabled)        score -= 8;
        if (!i.TelemetryDisabled)         score -= 12;
        score -= Math.Min(20, i.BloatwareCount * 1.5); // up to -20 for bloat

        return (int)Math.Clamp(score, 0, 100);
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~BoostScoreCalculatorTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add BoostScoreCalculator with clamped penalty scoring"
```

---

### Task 14: SystemSampler (1 Hz event stream)

**Files:**
- Create: `src/PrimeOSTuner.Core/Monitoring/SystemSample.cs`
- Create: `src/PrimeOSTuner.Core/Monitoring/SystemSampler.cs`
- Create: `src/PrimeOSTuner.Tests/Monitoring/SystemSamplerTests.cs`

- [ ] **Step 1: Write the failing test** (uses Moq + a fake hardware client)

`src/PrimeOSTuner.Tests/Monitoring/SystemSamplerTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Monitoring;

public class SystemSamplerTests
{
    [Fact]
    public async Task Sampler_emits_events_using_hardware_client_data()
    {
        var hw = new Mock<IHardwareClient>();
        hw.Setup(h => h.Snapshot()).Returns(new HardwareSnapshot(
            42, 8L * 1024 * 1024 * 1024, 16L * 1024 * 1024 * 1024, 30, 60, 100, 50));

        using var sampler = new SystemSampler(hw.Object, intervalMs: 50);
        var samples = new List<SystemSample>();
        sampler.Sampled += (_, s) => samples.Add(s);

        sampler.Start();
        await Task.Delay(200);
        sampler.Stop();

        samples.Should().NotBeEmpty();
        samples[0].CpuPercent.Should().Be(42);
        samples[0].RamPercent.Should().BeApproximately(50.0, 0.1);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~SystemSamplerTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Monitoring/SystemSample.cs`:

```csharp
namespace PrimeOSTuner.Core.Monitoring;

public sealed record SystemSample(
    DateTime TakenAtUtc,
    double CpuPercent,
    double RamPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    double GpuPercent,
    double GpuTempC,
    long NetworkDownBps,
    long NetworkUpBps);
```

`src/PrimeOSTuner.Core/Monitoring/SystemSampler.cs`:

```csharp
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Monitoring;

public sealed class SystemSampler : IDisposable
{
    private readonly IHardwareClient _hardware;
    private readonly System.Timers.Timer _timer;

    public event EventHandler<SystemSample>? Sampled;

    public SystemSampler(IHardwareClient hardware, int intervalMs = 1000)
    {
        _hardware = hardware;
        _timer = new System.Timers.Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        try
        {
            var s = _hardware.Snapshot();
            var ramPct = s.RamTotalBytes == 0 ? 0 : (double)s.RamUsedBytes / s.RamTotalBytes * 100.0;
            Sampled?.Invoke(this, new SystemSample(
                DateTime.UtcNow,
                s.CpuPercent, ramPct,
                s.RamUsedBytes, s.RamTotalBytes,
                s.GpuPercent, s.GpuTempC,
                s.NetworkDownBps, s.NetworkUpBps));
        }
        catch { /* one bad sample shouldn't kill the stream */ }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~SystemSamplerTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add SystemSampler emitting per-tick samples from IHardwareClient"
```

---

## Phase F — Optimize Tweaks

### Task 15: JunkFileTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/JunkFileTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/JunkFileTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/JunkFileTweakTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class JunkFileTweakTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"primeos-junk-{Guid.NewGuid()}");

    public JunkFileTweakTests() => Directory.CreateDirectory(_tempRoot);
    public void Dispose() { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); }

    [Fact]
    public async Task Apply_deletes_files_in_target_dirs_and_returns_freed_bytes()
    {
        var sub = Path.Combine(_tempRoot, "Temp");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "a.tmp"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(sub, "b.tmp"), new byte[2048]);

        var tweak = new JunkFileTweak(new[] { sub });

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("3072");
        Directory.GetFiles(sub).Should().BeEmpty();
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_junk_present()
    {
        var sub = Path.Combine(_tempRoot, "Temp");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "a.tmp"), new byte[100]);

        var tweak = new JunkFileTweak(new[] { sub });

        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~JunkFileTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/JunkFileTweak.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class JunkFileTweak : ITweak
{
    private readonly string[] _targetDirs;

    public string Id => "core.junk-files";
    public string DisplayName => "Clear junk files";
    public string Description => "Removes temp files, browser caches, and Windows update cache.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public JunkFileTweak() : this(DefaultTargets()) { }
    public JunkFileTweak(string[] targetDirs) { _targetDirs = targetDirs; }

    private static string[] DefaultTargets() =>
        new[]
        {
            Environment.ExpandEnvironmentVariables("%TEMP%"),
            Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Temp"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\Windows\INetCache"),
        };

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        long junk = 0;
        foreach (var dir in _targetDirs)
            if (Directory.Exists(dir))
                foreach (var f in EnumerateSafe(dir))
                    junk += SafeLength(f);

        return Task.FromResult(junk > 0 ? TweakState.NotApplied : TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        var totalDirs = _targetDirs.Length;
        for (int i = 0; i < totalDirs; i++)
        {
            ct.ThrowIfCancellationRequested();
            var dir = _targetDirs[i];
            if (!Directory.Exists(dir)) continue;

            foreach (var f in EnumerateSafe(dir))
            {
                try
                {
                    var len = SafeLength(f);
                    File.Delete(f);
                    freed += len;
                }
                catch { /* in-use file — skip */ }
            }
            progress?.Report((int)((i + 1) / (double)totalDirs * 100));
        }

        var undo = JsonSerializer.Serialize(new { freed });
        return Task.FromResult(TweakResult.Success(undo));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Junk file deletion cannot be reverted."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        long total = 0;
        foreach (var dir in _targetDirs)
            if (Directory.Exists(dir))
                foreach (var f in EnumerateSafe(dir))
                    total += SafeLength(f);
        return Task.FromResult($"Will delete approximately {total / 1024.0 / 1024.0:F1} MB across {_targetDirs.Length} folders.");
    }

    private static IEnumerable<string> EnumerateSafe(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~JunkFileTweakTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add JunkFileTweak (non-revertible safe cleanup)"
```

---

### Task 16: PowerPlanTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/PowerPlanTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/PowerPlanTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/PowerPlanTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class PowerPlanTweakTests
{
    private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid UltimateGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    [Fact]
    public async Task Apply_switches_to_ultimate_and_returns_previous_guid()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActivePlan()).Returns(new PowerPlan(BalancedGuid, "Balanced"));
        client.Setup(c => c.EnsureUltimatePerformancePlan()).Returns(UltimateGuid);

        var tweak = new PowerPlanTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain(BalancedGuid.ToString());
        client.Verify(c => c.SetActivePlan(UltimateGuid), Times.Once);
    }

    [Fact]
    public async Task Revert_sets_active_plan_back_to_undo_guid()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new PowerPlanTweak(client.Object);

        var result = await tweak.RevertAsync(BalancedGuid.ToString("D"));

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActivePlan(BalancedGuid), Times.Once);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~PowerPlanTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/PowerPlanTweak.cs`:

```csharp
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PowerPlanTweak : ITweak
{
    private readonly IPowerPlanClient _client;
    private static readonly Guid UltimateGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    public string Id => "core.power-plan";
    public string DisplayName => "Switch to Ultimate Performance power plan";
    public string Description => "Sets Windows to the Ultimate Performance power plan, prioritizing speed over efficiency.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public PowerPlanTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var active = _client.GetActivePlan();
        return Task.FromResult(active.Guid == UltimateGuid ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var previous = _client.GetActivePlan();
        var ultimate = _client.EnsureUltimatePerformancePlan();
        _client.SetActivePlan(ultimate);
        return Task.FromResult(TweakResult.Success(previous.Guid.ToString("D")));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!Guid.TryParse(undoData, out var previous))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));
        _client.SetActivePlan(previous);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var active = _client.GetActivePlan();
        return Task.FromResult($"Will switch active power plan from '{active.Name}' to Ultimate Performance.");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~PowerPlanTweakTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add PowerPlanTweak switching active plan to Ultimate Performance"
```

---

### Task 17: RamCleanerTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/RamCleanerTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/RamCleanerTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/RamCleanerTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RamCleanerTweakTests
{
    [Fact]
    public async Task Apply_calls_TrimAllUserProcesses()
    {
        var client = new Mock<IProcessClient>();
        client.Setup(c => c.TrimAllUserProcesses()).Returns(123);

        var tweak = new RamCleanerTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("123");
        client.Verify(c => c.TrimAllUserProcesses(), Times.Once);
    }

    [Fact]
    public async Task Probe_always_returns_NotApplied_since_RAM_refills()
    {
        var tweak = new RamCleanerTweak(Mock.Of<IProcessClient>());
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~RamCleanerTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/RamCleanerTweak.cs`:

```csharp
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : ITweak
{
    private readonly IProcessClient _processes;

    public string Id => "core.ram-cleaner";
    public string DisplayName => "Free idle RAM";
    public string Description => "Asks Windows to trim working sets of idle processes, returning unused memory to the available pool.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public RamCleanerTweak(IProcessClient processes) { _processes = processes; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var attempted = _processes.TrimAllUserProcesses();
        return Task.FromResult(TweakResult.Success($"{{\"attempted\":{attempted}}}"));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows will repopulate working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will request Windows to trim working sets of all accessible processes.");
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~RamCleanerTweakTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add RamCleanerTweak invoking ProcessClient.TrimAllUserProcesses"
```

---

### Task 18: VisualEffectsTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/VisualEffectsTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/VisualEffectsTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/VisualEffectsTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class VisualEffectsTweakTests
{
    [Fact]
    public async Task Apply_writes_VisualFXSetting_to_2_and_returns_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(
            RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting",
            "2"))
        .Returns(new RegistryBackup(
            RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting",
            "0"));

        var tweak = new VisualEffectsTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("VisualFXSetting");
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~VisualEffectsTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/VisualEffectsTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class VisualEffectsTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string ValueName = "VisualFXSetting";

    private readonly IRegistryClient _registry;

    public string Id => "core.visual-effects";
    public string DisplayName => "Optimize visual effects for performance";
    public string Description => "Disables animations, transparency, and shadows to free GPU/CPU cycles.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public VisualEffectsTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.CurrentUser, SubKey, ValueName);
        return Task.FromResult(v == "2" ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.CurrentUser, SubKey, ValueName, "2");
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
    {
        var current = _registry.ReadString(RegistryHive.CurrentUser, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKCU\\{SubKey}\\{ValueName} from '{current}' to '2' (best performance).");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~VisualEffectsTweakTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add VisualEffectsTweak setting VisualFXSetting=2 with registry backup"
```

---

## Phase G — Pipeline

### Task 19: OneClickOptimizer

**Files:**
- Create: `src/PrimeOSTuner.Core/Pipeline/OneClickOptimizer.cs`
- Create: `src/PrimeOSTuner.Core/Pipeline/OptimizeReport.cs`
- Create: `src/PrimeOSTuner.Tests/Pipeline/OneClickOptimizerTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/PrimeOSTuner.Tests/Pipeline/OneClickOptimizerTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Pipeline;

public class OneClickOptimizerTests
{
    private static Mock<ITweak> StubTweak(string id, bool succeeds = true)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.SetupGet(t => t.IsDestructive).Returns(false);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(succeeds ? TweakResult.Success("undo") : TweakResult.Failure("nope"));
        return m;
    }

    [Fact]
    public async Task Run_applies_all_safe_tweaks_and_records_each_in_history()
    {
        var tweaks = new[] { StubTweak("a").Object, StubTweak("b").Object };
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var history = new TweakHistory(historyPath);

        var optimizer = new OneClickOptimizer(tweaks, history);
        var report = await optimizer.RunAsync();

        report.SuccessCount.Should().Be(2);
        (await history.LoadAsync()).Should().HaveCount(2);

        File.Delete(historyPath);
    }

    [Fact]
    public async Task Run_skips_destructive_tweaks()
    {
        var safe = StubTweak("safe").Object;
        var destructive = StubTweak("danger");
        destructive.SetupGet(t => t.IsDestructive).Returns(true);

        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var optimizer = new OneClickOptimizer(new[] { safe, destructive.Object }, new TweakHistory(historyPath));

        var report = await optimizer.RunAsync();

        report.AppliedTweakIds.Should().BeEquivalentTo(new[] { "safe" });
        report.SkippedDestructiveCount.Should().Be(1);

        File.Delete(historyPath);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~OneClickOptimizerTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Pipeline/OptimizeReport.cs`:

```csharp
namespace PrimeOSTuner.Core.Pipeline;

public sealed record OptimizeReport(
    int SuccessCount,
    int FailureCount,
    int SkippedDestructiveCount,
    IReadOnlyList<string> AppliedTweakIds,
    IReadOnlyList<(string TweakId, string Error)> Failures);
```

`src/PrimeOSTuner.Core/Pipeline/OneClickOptimizer.cs`:

```csharp
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Pipeline;

public sealed class OneClickOptimizer
{
    private readonly IReadOnlyList<ITweak> _tweaks;
    private readonly TweakHistory _history;

    public OneClickOptimizer(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        _tweaks = tweaks.ToList();
        _history = history;
    }

    public async Task<OptimizeReport> RunAsync(IProgress<(int Done, int Total, string CurrentName)>? progress = null, CancellationToken ct = default)
    {
        var safe = _tweaks.Where(t => !t.IsDestructive).ToList();
        var skippedDestructive = _tweaks.Count - safe.Count;
        var applied = new List<string>();
        var failures = new List<(string, string)>();
        int success = 0, failure = 0;

        for (int i = 0; i < safe.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = safe[i];
            progress?.Report((i, safe.Count, t.DisplayName));
            var result = await t.ApplyAsync(null, ct);
            if (result.Succeeded)
            {
                success++;
                applied.Add(t.Id);
                await _history.AppendAsync(new HistoryEntry(
                    Guid.NewGuid(), t.Id, t.DisplayName, DateTime.UtcNow, result.UndoData, false));
            }
            else
            {
                failure++;
                failures.Add((t.Id, result.Error ?? "unknown"));
            }
        }

        progress?.Report((safe.Count, safe.Count, "Done"));
        return new OptimizeReport(success, failure, skippedDestructive, applied, failures);
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~OneClickOptimizerTests
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add OneClickOptimizer running safe tweaks and recording history"
```

---

## Phase H — UI Shell

### Task 20: Theme resources (colors + base styles)

**Files:**
- Create: `src/PrimeOSTuner.UI/Theme/Colors.xaml`
- Create: `src/PrimeOSTuner.UI/Theme/Styles.xaml`
- Modify: `src/PrimeOSTuner.UI/App.xaml`

- [ ] **Step 1: Create `Colors.xaml`** (the design tokens)

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="Bg0Color">#06080c</Color>
    <Color x:Key="Bg1Color">#0b0f17</Color>
    <Color x:Key="Bg2Color">#11161f</Color>
    <Color x:Key="Bg3Color">#1a2030</Color>
    <Color x:Key="LineColor">#1c2333</Color>
    <Color x:Key="Text0Color">#f1f5fb</Color>
    <Color x:Key="Text1Color">#c7cfde</Color>
    <Color x:Key="Text2Color">#8b95a8</Color>
    <Color x:Key="Text3Color">#5a6478</Color>
    <Color x:Key="AccentColor">#00e5c5</Color>
    <Color x:Key="Accent2Color">#6ad7ff</Color>
    <Color x:Key="GoodColor">#43d27a</Color>
    <Color x:Key="WarnColor">#ffb84d</Color>
    <Color x:Key="DangerColor">#ff6b6b</Color>

    <SolidColorBrush x:Key="Bg0Brush"   Color="{StaticResource Bg0Color}"/>
    <SolidColorBrush x:Key="Bg1Brush"   Color="{StaticResource Bg1Color}"/>
    <SolidColorBrush x:Key="Bg2Brush"   Color="{StaticResource Bg2Color}"/>
    <SolidColorBrush x:Key="LineBrush"  Color="{StaticResource LineColor}"/>
    <SolidColorBrush x:Key="Text0Brush" Color="{StaticResource Text0Color}"/>
    <SolidColorBrush x:Key="Text1Brush" Color="{StaticResource Text1Color}"/>
    <SolidColorBrush x:Key="Text2Brush" Color="{StaticResource Text2Color}"/>
    <SolidColorBrush x:Key="Text3Brush" Color="{StaticResource Text3Color}"/>
    <SolidColorBrush x:Key="AccentBrush"  Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="Accent2Brush" Color="{StaticResource Accent2Color}"/>
    <SolidColorBrush x:Key="GoodBrush"   Color="{StaticResource GoodColor}"/>
</ResourceDictionary>
```

- [ ] **Step 2: Create `Styles.xaml`** (base text/button styles)

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource Bg0Brush}"/>
        <Setter Property="Foreground" Value="{StaticResource Text0Brush}"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
        <Setter Property="FontSize" Value="13"/>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text1Brush}"/>
    </Style>

    <Style x:Key="HeaderText" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text0Brush}"/>
        <Setter Property="FontSize" Value="20"/>
        <Setter Property="FontWeight" Value="Bold"/>
    </Style>

    <Style x:Key="SectionLabel" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text3Brush}"/>
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
        <Setter Property="Margin" Value="0,0,0,6"/>
    </Style>

    <Style x:Key="CardBorder" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Bg2Brush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource LineBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="14"/>
        <Setter Property="Padding" Value="18"/>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 3: Wire resources into `App.xaml`**

```xml
<Application x:Class="PrimeOSTuner.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Theme/Colors.xaml"/>
                <ResourceDictionary Source="Theme/Styles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Build to confirm XAML compiles**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add design-token theme resources (colors, brushes, base styles)"
```

---

### Task 21: Shell — MainWindow with sidebar nav

**Files:**
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`
- Create: `src/PrimeOSTuner.UI/ViewModels/ShellViewModel.cs`

- [ ] **Step 1: Create `ShellViewModel.cs`** (handles tab selection)

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrimeOSTuner.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] private string _activeTab = "Dashboard";

    [RelayCommand]
    private void Navigate(string tab) => ActiveTab = tab;
}
```

- [ ] **Step 2: Replace `MainWindow.xaml` with sidebar layout**

```xml
<Window x:Class="PrimeOSTuner.UI.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PrimeOS Tuner"
        Height="780" Width="1240"
        WindowStartupLocation="CenterScreen">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Sidebar -->
        <Border Grid.Column="0" Background="{StaticResource Bg1Brush}" BorderBrush="{StaticResource LineBrush}" BorderThickness="0,0,1,0">
            <StackPanel Margin="12,20">
                <TextBlock Text="PRIMEOS TUNER" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}" Margin="6,0,0,16"/>
                <TextBlock Text="NAVIGATION" Style="{StaticResource SectionLabel}" Margin="6,0,0,8"/>
                <Button Content="⌂  Dashboard" Tag="Dashboard" Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                <Button Content="⚡  Optimize"  Tag="Optimize"  Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                <Button Content="⛨  History"   Tag="History"   Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
            </StackPanel>
        </Border>

        <!-- Page host -->
        <ContentControl Grid.Column="1" x:Name="PageHost" Margin="32,28"/>
    </Grid>
</Window>
```

- [ ] **Step 3: Add the NavButtonStyle** to `Theme/Styles.xaml` (append before `</ResourceDictionary>`)

```xml
<Style x:Key="NavButtonStyle" TargetType="Button">
    <Setter Property="HorizontalContentAlignment" Value="Left"/>
    <Setter Property="Padding" Value="14,11"/>
    <Setter Property="Margin" Value="0,2"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="{StaticResource Text1Brush}"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="bg" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="bg" Property="Background" Value="#10FFFFFF"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- [ ] **Step 4: Update `MainWindow.xaml.cs`** with code-behind (we'll wire DI in Task 22)

```csharp
using System.Windows;
using System.Windows.Controls;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowPlaceholder("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowPlaceholder(tab);
    }

    private void ShowPlaceholder(string tab)
    {
        PageHost.Content = new TextBlock
        {
            Text = $"{tab} (placeholder)",
            FontSize = 22,
            Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
        };
    }
}
```

- [ ] **Step 5: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: a window with a left sidebar showing 3 nav buttons. Clicking each replaces the right side with the tab name.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add MainWindow shell with sidebar navigation"
```

---

### Task 22: DI bootstrap with Generic Host

**Files:**
- Modify: `src/PrimeOSTuner.UI/App.xaml`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Remove `StartupUri` from `App.xaml`** (we'll create MainWindow ourselves)

```xml
<Application x:Class="PrimeOSTuner.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Theme/Colors.xaml"/>
                <ResourceDictionary Source="Theme/Styles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Replace `App.xaml.cs`** with host bootstrap

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using Serilog;
using System.IO;
using System.Windows;

namespace PrimeOSTuner.UI;

public partial class App : Application
{
    public IHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeOSTuner", "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logsDir, "primeos-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(s =>
            {
                // Win layer
                s.AddSingleton<IRegistryClient, RegistryClient>();
                s.AddSingleton<IProcessClient, ProcessClient>();
                s.AddSingleton<IPowerPlanClient, PowerPlanClient>();
                s.AddSingleton<IRestorePointClient, RestorePointClient>();
                s.AddSingleton<IHardwareClient, HardwareClient>();

                // Core
                s.AddSingleton(_ => new TweakHistory(TweakHistory.DefaultPath()));
                s.AddSingleton<SystemSampler>();
                s.AddSingleton<JunkFileTweak>();
                s.AddSingleton<PowerPlanTweak>();
                s.AddSingleton<RamCleanerTweak>();
                s.AddSingleton<VisualEffectsTweak>();
                s.AddSingleton<IEnumerable<ITweak>>(sp => new ITweak[]
                {
                    sp.GetRequiredService<JunkFileTweak>(),
                    sp.GetRequiredService<PowerPlanTweak>(),
                    sp.GetRequiredService<RamCleanerTweak>(),
                    sp.GetRequiredService<VisualEffectsTweak>()
                });
                s.AddSingleton<OneClickOptimizer>();

                // ViewModels & MainWindow
                s.AddSingleton<ShellViewModel>();
                s.AddSingleton<MainWindow>();
            })
            .Build();

        Host.Start();
        var window = Host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        Host?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        Host?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Update `MainWindow.xaml.cs` constructor** to accept the ShellViewModel

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        ShowPlaceholder("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowPlaceholder(tab);
    }

    private void ShowPlaceholder(string tab)
    {
        PageHost.Content = new TextBlock
        {
            Text = $"{tab} (placeholder)",
            FontSize = 22,
            Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
        };
    }
}
```

- [ ] **Step 4: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: Same UI as before, but check `%LOCALAPPDATA%\PrimeOSTuner\logs\` — a log file should now exist.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add Generic Host DI bootstrap with Serilog file/console sinks"
```

---

## Phase I — Dashboard

### Task 23: DashboardViewModel

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs`
- Create: `src/PrimeOSTuner.Tests/ViewModels/DashboardViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/ViewModels/DashboardViewModelTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public void OnSampled_updates_observable_properties()
    {
        var hw = new Mock<IHardwareClient>();
        hw.Setup(h => h.Snapshot()).Returns(new HardwareSnapshot(50, 4_000_000_000, 16_000_000_000, 25, 70, 100, 50));
        using var sampler = new SystemSampler(hw.Object, 50);

        var vm = new DashboardViewModel(sampler);
        sampler.Start();
        Thread.Sleep(150);
        sampler.Stop();

        vm.CpuPercent.Should().BeGreaterThan(0);
        vm.RamPercent.Should().BeApproximately(25.0, 0.5);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~DashboardViewModelTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Monitoring;

namespace PrimeOSTuner.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SystemSampler _sampler;

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private long _ramUsedBytes;
    [ObservableProperty] private long _ramTotalBytes;
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private double _gpuTempC;
    [ObservableProperty] private long _networkDownBps;
    [ObservableProperty] private long _networkUpBps;

    public ObservableCollection<double> CpuHistory { get; } = new();
    public ObservableCollection<double> RamHistory { get; } = new();
    public ObservableCollection<double> GpuHistory { get; } = new();
    public ObservableCollection<double> NetHistory { get; } = new();

    public DashboardViewModel(SystemSampler sampler)
    {
        _sampler = sampler;
        _sampler.Sampled += OnSampled;
        _sampler.Start();
    }

    private void OnSampled(object? sender, SystemSample s)
    {
        // Marshal to UI thread when running under WPF; tests run synchronously on test thread.
        var dispatcher = Application.Current?.Dispatcher;
        Action update = () =>
        {
            CpuPercent = s.CpuPercent;
            RamPercent = s.RamPercent;
            RamUsedBytes = s.RamUsedBytes;
            RamTotalBytes = s.RamTotalBytes;
            GpuPercent = s.GpuPercent;
            GpuTempC = s.GpuTempC;
            NetworkDownBps = s.NetworkDownBps;
            NetworkUpBps = s.NetworkUpBps;
            Push(CpuHistory, s.CpuPercent);
            Push(RamHistory, s.RamPercent);
            Push(GpuHistory, s.GpuPercent);
            Push(NetHistory, Math.Min(100, (s.NetworkDownBps + s.NetworkUpBps) / 1_000_000.0));
        };
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.Invoke(update);
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 60) series.RemoveAt(0);
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _sampler.Stop();
    }
}
```

- [ ] **Step 4: Register in DI** — add to `App.xaml.cs` `ConfigureServices`:

```csharp
s.AddSingleton<DashboardViewModel>();
```

- [ ] **Step 5: Run, watch test pass**

```powershell
dotnet test --filter FullyQualifiedName~DashboardViewModelTests
```

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add DashboardViewModel subscribing to SystemSampler"
```

---

### Task 24: BoostScoreRing user control

**Files:**
- Create: `src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml`
- Create: `src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml.cs`

- [ ] **Step 1: Create the UserControl XAML**

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.BoostScoreRing"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="120" Height="120">
    <Grid>
        <Ellipse Width="120" Height="120" Stroke="{StaticResource LineBrush}" StrokeThickness="10"/>
        <Path x:Name="Arc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round">
            <Path.Stroke>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="{StaticResource AccentColor}" Offset="0"/>
                    <GradientStop Color="{StaticResource Accent2Color}" Offset="1"/>
                </LinearGradientBrush>
            </Path.Stroke>
        </Path>
        <TextBlock x:Name="ScoreText" Text="0" FontSize="34" FontWeight="Bold"
                   Foreground="{StaticResource Text0Brush}"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Implement code-behind with a `Score` dependency property**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PrimeOSTuner.UI.Controls;

public partial class BoostScoreRing : UserControl
{
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(nameof(Score), typeof(int), typeof(BoostScoreRing),
            new PropertyMetadata(0, OnScoreChanged));

    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public BoostScoreRing()
    {
        InitializeComponent();
        UpdateArc(0);
    }

    private static void OnScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BoostScoreRing r && e.NewValue is int v)
        {
            r.ScoreText.Text = v.ToString();
            r.UpdateArc(v);
        }
    }

    private void UpdateArc(int score)
    {
        score = Math.Clamp(score, 0, 100);
        const double cx = 60, cy = 60, r = 55;
        var angle = score / 100.0 * 360.0;
        var rad = (angle - 90) * Math.PI / 180.0;
        var endX = cx + r * Math.Cos(rad);
        var endY = cy + r * Math.Sin(rad);
        var largeArc = angle > 180 ? 1 : 0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(cx, cy - r), isFilled: false, isClosed: false);
            ctx.ArcTo(new Point(endX, endY),
                new Size(r, r), 0, largeArc == 1, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        Arc.Data = geometry;
    }
}
```

- [ ] **Step 3: Build to confirm**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "Add BoostScoreRing user control with arc rendered from Score property"
```

---

### Task 25: StatCard user control with sparkline

**Files:**
- Create: `src/PrimeOSTuner.UI/Controls/StatCard.xaml`
- Create: `src/PrimeOSTuner.UI/Controls/StatCard.xaml.cs`

- [ ] **Step 1: Create the UserControl XAML** (uses LiveCharts2)

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.StatCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF">
    <Border Style="{StaticResource CardBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            <TextBlock x:Name="NameText" Grid.Row="0" Style="{StaticResource SectionLabel}"/>
            <TextBlock x:Name="ValueText" Grid.Row="1" FontSize="28" FontWeight="Bold" Foreground="{StaticResource Text0Brush}"/>
            <TextBlock x:Name="SubText" Grid.Row="2" FontSize="11" Foreground="{StaticResource Text3Brush}"/>
            <lvc:CartesianChart x:Name="Spark" Grid.Row="3" Height="40" Margin="0,8,0,0"
                                AnimationsSpeed="0:0:0.3"
                                TooltipPosition="Hidden"/>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: Implement code-behind**

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace PrimeOSTuner.UI.Controls;

public partial class StatCard : UserControl
{
    public static readonly DependencyProperty StatNameProperty =
        DependencyProperty.Register(nameof(StatName), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).NameText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueTextProperty2 =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).ValueText.Text = (string)e.NewValue));

    public static readonly DependencyProperty SubTextProperty =
        DependencyProperty.Register(nameof(SubText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).SubText.Text = (string)e.NewValue));

    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(ObservableCollection<double>), typeof(StatCard),
            new PropertyMetadata(null, OnHistoryChanged));

    public string StatName { get => (string)GetValue(StatNameProperty); set => SetValue(StatNameProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty2); set => SetValue(ValueTextProperty2, value); }
    public string SubText { get => (string)GetValue(SubTextProperty); set => SetValue(SubTextProperty, value); }
    public ObservableCollection<double> History { get => (ObservableCollection<double>)GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }

    public StatCard()
    {
        InitializeComponent();
        Spark.XAxes = new[] { new Axis { IsVisible = false } };
        Spark.YAxes = new[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } };
    }

    private static void OnHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        if (e.NewValue is ObservableCollection<double> coll)
        {
            card.Spark.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = coll,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(new SKColor(0, 229, 197)) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(new SKColor(0, 229, 197, 80)),
                    LineSmoothness = 0.4
                }
            };
        }
    }
}
```

- [ ] **Step 3: Build to confirm**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "Add StatCard user control with LiveCharts2 sparkline"
```

---

### Task 26: DashboardView XAML

**Files:**
- Create: `src/PrimeOSTuner.UI/Views/DashboardView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/DashboardView.xaml.cs`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs` — load DashboardView from DI

- [ ] **Step 1: Create `DashboardView.xaml`**

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.DashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:c="clr-namespace:PrimeOSTuner.UI.Controls">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Dashboard" Style="{StaticResource HeaderText}" Margin="0,0,0,18"/>

        <!-- Hero row: score + optimize -->
        <Grid Grid.Row="1" Margin="0,0,0,18">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="320"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Border Grid.Column="0" Style="{StaticResource CardBorder}" Margin="0,0,12,0">
                <StackPanel Orientation="Horizontal">
                    <c:BoostScoreRing Score="87" Margin="0,0,16,0"/>
                    <StackPanel VerticalAlignment="Center">
                        <TextBlock Text="BOOST SCORE" Style="{StaticResource SectionLabel}"/>
                        <TextBlock Text="EXCELLENT" Foreground="{StaticResource AccentBrush}" FontWeight="Bold" FontSize="14"/>
                        <TextBlock Text="↗ +12 since last scan" Foreground="{StaticResource Text2Brush}" FontSize="11" Margin="0,4,0,0"/>
                    </StackPanel>
                </StackPanel>
            </Border>

            <Border Grid.Column="1" Style="{StaticResource CardBorder}" BorderBrush="{StaticResource AccentBrush}">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" VerticalAlignment="Center">
                        <TextBlock Text="One-Click Optimize" Style="{StaticResource HeaderText}" FontSize="20"/>
                        <TextBlock Text="4 safe tweaks ready · auto-creates restore point" Foreground="{StaticResource Text2Brush}" FontSize="12"/>
                    </StackPanel>
                    <Button Grid.Column="1" x:Name="OptimizeButton" Content="⚡ OPTIMIZE NOW" Click="OptimizeButton_Click"
                            Padding="22,12" FontWeight="Bold" Foreground="#001b17" Background="{StaticResource AccentBrush}"
                            BorderThickness="0" Cursor="Hand"/>
                </Grid>
            </Border>
        </Grid>

        <!-- Stats row -->
        <UniformGrid Grid.Row="2" Columns="4" Rows="1" Margin="0,0,0,18">
            <c:StatCard Margin="0,0,6,0"  StatName="CPU"     ValueText="{Binding CpuPercent, StringFormat='{}{0:F0}%'}" SubText="" History="{Binding CpuHistory}"/>
            <c:StatCard Margin="6,0"      StatName="MEMORY"  ValueText="{Binding RamPercent, StringFormat='{}{0:F0}%'}" SubText="" History="{Binding RamHistory}"/>
            <c:StatCard Margin="6,0"      StatName="GPU"     ValueText="{Binding GpuPercent, StringFormat='{}{0:F0}%'}" SubText="" History="{Binding GpuHistory}"/>
            <c:StatCard Margin="6,0,0,0"  StatName="NETWORK" ValueText="{Binding NetworkDownBps, StringFormat='{}{0:N0} Bps'}" SubText="" History="{Binding NetHistory}"/>
        </UniformGrid>

        <!-- Activity placeholder (real history view comes later) -->
        <Border Grid.Row="3" Style="{StaticResource CardBorder}">
            <StackPanel>
                <TextBlock Text="RECENT ACTIVITY" Style="{StaticResource SectionLabel}"/>
                <TextBlock x:Name="ActivityPlaceholder" Text="Run an optimization to see activity here." Foreground="{StaticResource Text3Brush}" FontSize="12"/>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Implement code-behind**

`src/PrimeOSTuner.UI/Views/DashboardView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly OneClickOptimizer _optimizer;

    public DashboardView(DashboardViewModel vm, OneClickOptimizer optimizer)
    {
        InitializeComponent();
        DataContext = vm;
        _optimizer = optimizer;
    }

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        OptimizeButton.IsEnabled = false;
        OptimizeButton.Content = "Working…";
        try
        {
            var report = await _optimizer.RunAsync();
            ActivityPlaceholder.Text =
                $"Optimization complete: {report.SuccessCount} succeeded, {report.FailureCount} failed.";
        }
        catch (Exception ex)
        {
            ActivityPlaceholder.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            OptimizeButton.IsEnabled = true;
            OptimizeButton.Content = "⚡ OPTIMIZE NOW";
        }
    }
}
```

- [ ] **Step 3: Register `DashboardView` in DI** — add to `App.xaml.cs` `ConfigureServices`:

```csharp
s.AddTransient<Views.DashboardView>();
```

- [ ] **Step 4: Update `MainWindow.xaml.cs`** to resolve views from the host

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        ShowTab("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowTab(tab);
    }

    private void ShowTab(string tab)
    {
        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard" => sp.GetRequiredService<DashboardView>(),
            _ => new TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22,
                Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
            }
        };
    }
}
```

- [ ] **Step 5: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: Dashboard tab shows the score ring, the One-Click Optimize button, and 4 stat cards with live numbers updating each second. CPU/RAM/GPU/Network values change as you move things around. Don't click Optimize yet.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add DashboardView wiring score, optimize button, and live stat cards"
```

---

### Task 27: Optimize tab placeholder

**Files:**
- Create: `src/PrimeOSTuner.UI/Views/OptimizeView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/OptimizeView.xaml.cs`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Create `OptimizeView.xaml`** — a simple list of tweaks with Apply/Preview per item

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.OptimizeView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="Optimize" Style="{StaticResource HeaderText}" Margin="0,0,0,18"/>
        <ItemsControl Grid.Row="1" x:Name="TweakList">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Border Style="{StaticResource CardBorder}" Margin="0,0,0,8">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="{Binding DisplayName}" Foreground="{StaticResource Text0Brush}" FontWeight="Bold"/>
                                <TextBlock Text="{Binding Description}" Foreground="{StaticResource Text2Brush}" FontSize="11" Margin="0,4,0,0" TextWrapping="Wrap"/>
                            </StackPanel>
                            <Button Grid.Column="1" Content="Preview" Tag="{Binding}" Click="PreviewClick"
                                    Padding="14,6" Margin="8,0" Background="Transparent" Foreground="{StaticResource Text1Brush}"
                                    BorderBrush="{StaticResource LineBrush}" BorderThickness="1"/>
                            <Button Grid.Column="2" Content="Apply" Tag="{Binding}" Click="ApplyClick"
                                    Padding="14,6" Background="{StaticResource AccentBrush}" Foreground="#001b17"
                                    BorderThickness="0" FontWeight="Bold"/>
                        </Grid>
                    </Border>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Implement code-behind**

`src/PrimeOSTuner.UI/Views/OptimizeView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.Views;

public partial class OptimizeView : UserControl
{
    private readonly TweakHistory _history;

    public OptimizeView(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        InitializeComponent();
        TweakList.ItemsSource = tweaks.Where(t => !t.IsDestructive).ToList();
        _history = history;
    }

    private async void PreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t })
        {
            var preview = await t.PreviewAsync();
            MessageBox.Show(preview, $"Preview — {t.DisplayName}");
        }
    }

    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t } btn)
        {
            btn.IsEnabled = false;
            try
            {
                var result = await t.ApplyAsync();
                if (result.Succeeded)
                {
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), t.Id, t.DisplayName, DateTime.UtcNow, result.UndoData, false));
                    MessageBox.Show("Applied successfully.", t.DisplayName);
                }
                else
                {
                    MessageBox.Show($"Failed: {result.Error}", t.DisplayName);
                }
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}
```

- [ ] **Step 3: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddTransient<Views.OptimizeView>();
```

- [ ] **Step 4: Add to nav routing** in `MainWindow.xaml.cs`'s `ShowTab` switch:

```csharp
"Optimize" => sp.GetRequiredService<OptimizeView>(),
```

- [ ] **Step 5: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: Click "Optimize" in the sidebar — see four cards listing each tweak with Preview/Apply buttons. Click Preview on Junk Files — a dialog shows the bytes that will be deleted. **Do NOT click Apply yet on your real machine** — only in the VM.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add OptimizeView listing tweaks with per-item Preview/Apply"
```

---

### Task 28: HistoryView (basic)

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/HistoryViewModel.cs`
- Create: `src/PrimeOSTuner.UI/Views/HistoryView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/HistoryView.xaml.cs`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`, `App.xaml.cs`

- [ ] **Step 1: Create `HistoryViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.History;

namespace PrimeOSTuner.UI.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly TweakHistory _history;

    [ObservableProperty] private ObservableCollection<HistoryEntry> _entries = new();

    public HistoryViewModel(TweakHistory history) { _history = history; }

    public async Task LoadAsync()
    {
        var entries = await _history.LoadAsync();
        Entries = new ObservableCollection<HistoryEntry>(entries.Reverse());
    }
}
```

- [ ] **Step 2: Create `HistoryView.xaml`**

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.HistoryView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="History" Style="{StaticResource HeaderText}" Margin="0,0,0,18"/>
        <ItemsControl Grid.Row="1" ItemsSource="{Binding Entries}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Border Style="{StaticResource CardBorder}" Margin="0,0,0,8">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="{Binding DisplayName}" Foreground="{StaticResource Text0Brush}" FontWeight="Bold"/>
                                <TextBlock Foreground="{StaticResource Text3Brush}" FontSize="11" Margin="0,4,0,0">
                                    <Run Text="{Binding TweakId}"/>
                                    <Run Text=" · "/>
                                    <Run Text="{Binding AppliedAtUtc, StringFormat='{}{0:yyyy-MM-dd HH:mm} UTC'}"/>
                                </TextBlock>
                            </StackPanel>
                            <TextBlock Grid.Column="1" Text="reverted" Foreground="{StaticResource Text3Brush}" FontStyle="Italic"
                                       Visibility="{Binding Reverted, Converter={StaticResource BoolToVisibility}}"/>
                        </Grid>
                    </Border>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Add a `BoolToVisibility` converter** to `App.xaml` resources (replace the existing `<ResourceDictionary>` block):

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Theme/Colors.xaml"/>
            <ResourceDictionary Source="Theme/Styles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
    </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 4: Implement code-behind**

`src/PrimeOSTuner.UI/Views/HistoryView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class HistoryView : UserControl
{
    public HistoryView(HistoryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
```

- [ ] **Step 5: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddTransient<HistoryViewModel>();
s.AddTransient<Views.HistoryView>();
```

- [ ] **Step 6: Add to nav routing** — `MainWindow.xaml.cs`'s `ShowTab` switch:

```csharp
"History" => sp.GetRequiredService<HistoryView>(),
```

- [ ] **Step 7: Run the app**

```powershell
dotnet run --project src/PrimeOSTuner.UI
```

Expected: History tab shows "no entries" until you run an optimization in the VM.

- [ ] **Step 8: Commit**

```powershell
git add .
git commit -m "Add HistoryView listing tweak entries from TweakHistory"
```

---

## Phase J — End-to-end verification

### Task 29: VM smoke test

This is a manual end-to-end test, not a code task. No commit at the end unless changes are needed.

- [ ] **Step 1: Restore your VM to the `clean-baseline` snapshot**

In VirtualBox: select VM → Snapshots → right-click `clean-baseline` → Restore.

- [ ] **Step 2: Build a single-file release inside the VM** — easiest path is to build on host and copy.

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.1
```

- [ ] **Step 3: Copy `publish/v0.1` into the VM** (shared folder, drag-drop, or scp)

- [ ] **Step 4: In the VM, run `PrimeOSTuner.UI.exe`**

Expected: app opens; Dashboard shows live stats; navigation works.

- [ ] **Step 5: Click Optimize tab → Preview each of the four tweaks**

Expected: each preview dialog shows a sensible message.

- [ ] **Step 6: Apply Junk File tweak**

Expected: dialog says "Applied successfully"; History tab shows one entry.

- [ ] **Step 7: Apply Power Plan tweak as administrator**

Right-click `PrimeOSTuner.UI.exe` → "Run as administrator". Re-apply Power Plan tweak.

Verify: `powercfg /getactivescheme` in the VM's PowerShell shows "Ultimate Performance" or similar.

- [ ] **Step 8: Apply RAM Cleaner**

Expected: succeeds. Open Task Manager — committed memory should drop slightly.

- [ ] **Step 9: Apply Visual Effects**

Expected: succeeds. Open `SystemPropertiesPerformance.exe` in VM — radio is on "Adjust for best performance".

- [ ] **Step 10: Click Dashboard → One-Click Optimize**

Expected: completes successfully (since most tweaks are now already applied, side effects should be minor).

- [ ] **Step 11: Open History tab**

Expected: see entries for each tweak applied.

- [ ] **Step 12: Verify logs**

In the VM: `%LOCALAPPDATA%\PrimeOSTuner\logs\primeos-YYYYMMDD.log` — confirm tweaks recorded with no exceptions.

- [ ] **Step 13: Restore the VM snapshot**

Important — don't keep the modified state for the next run.

- [ ] **Step 14: If anything failed**, write a bug fix task before continuing. Don't skip past failures.

---

### Task 30: Tag v0.1.0

- [ ] **Step 1: Tag the commit**

```powershell
git tag -a v0.1.0 -m "v0.1.0 — Foundation + Dashboard + Optimize"
git log --oneline -1
```

Expected: shows the latest commit and the tag.

- [ ] **Step 2: Update memory** (telling future-you this milestone shipped)

Add a one-line entry to `C:\Users\jaxso\.claude\projects\C--Users-jaxso-projects-PC-Performance-booster\memory\MEMORY.md`:

```
- [Project: v0.1 shipped](project_primeos_tuner.md) — see project memory; v0.1 tagged 2026-MM-DD
```

(Or amend `project_primeos_tuner.md` with a "Status" section.)

---

## Done

You now have:
- A working WPF app with a Hone-style dark dashboard
- Live system monitoring with sparklines
- Four safe optimization tweaks (Junk, Power, RAM, Visual Effects)
- One-click optimization that applies all four with history tracking
- A history view with audit trail
- An end-to-end VM-tested release tagged v0.1.0

**Next plan:** v0.2 — Game Boost tab (mouse accel, timer resolution, Game Mode, GPU prefs, TCP tweaks). Will be drafted when v0.1 is fully shipped and you've confirmed it works in your environment.
