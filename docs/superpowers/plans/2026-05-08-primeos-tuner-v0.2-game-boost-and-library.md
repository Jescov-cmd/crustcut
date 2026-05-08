# PrimeOS Tuner v0.2 — Game Boost + Mode Profiles + Game Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v0.2 of PrimeOS Tuner — adds 9 new game-focused tweaks, a three-tier Mode profile system (Basic/Performance/Custom), a Steam-aware game library with cover art from SteamGridDB, and a background watcher that auto-applies a chosen profile when a known game launches and reverts it when the game closes.

**Architecture:** Build directly on the v0.1 4-project layout (UI → Core → Win + Tests). New `ITweak` implementations slot into the existing pipeline. A `ModeProfile` record bundles tweak IDs into named presets, applied via a new `ProfileApplier`. The Win layer gains a Steam library scanner and a SteamGridDB HTTP client. A `GameProcessWatcher` polls running processes, and `ProfileLifecycleService` ties watcher events to profile apply/revert with crash-safe persistence.

**Tech Stack:** Same as v0.1 — .NET 8, WPF, CommunityToolkit.Mvvm, Serilog, xUnit, Moq. New: `Gameloop.Vdf` for parsing Steam library files, `Microsoft.Extensions.Http` for typed `HttpClient` injection, SteamGridDB REST API.

---

## Working with this plan

This plan is a strict continuation of `2026-05-07-primeos-tuner-v0.1-foundation-and-dashboard.md`. It assumes the v0.1.0 tag is on `main` and the v0.1 file layout exists. Work top-to-bottom, one task at a time. Don't skip ahead — later tasks reference types created in earlier tasks.

- **Check off** the box on every step as you finish it (in your editor, replace `[ ]` with `[x]`).
- **Run the exact command shown.** If output differs from the "Expected" line, stop and ask before continuing.
- **Commit at the end of every task.** Many small commits are better than a few big ones — they're your undo button.
- **TDD rule** (for Core/Win/ViewModel tasks): write the test, watch it fail, write code, watch it pass, commit. Resist the urge to write code first.
- **VM rule**: any task that hits the registry, services, system files, or installs games must be run inside the Windows 11 VM, never on your real PC, until v0.2 is fully smoke-tested.
- **Network rule**: SteamGridDB calls hit the public internet. The integration tests are tagged `[Trait("Category", "Network")]` so you can opt-out via `--filter "Category!=Network"` if you're offline.

---

## Prerequisites (one-time setup before Task 1)

Do these once before Task 1.

- [ ] **Confirm v0.1.0 is tagged**

```powershell
git tag --list "v0.1.0"
```

Expected: prints `v0.1.0`. If empty, finish v0.1 first — do not start v0.2.

- [ ] **Confirm working directory and clean tree**

```powershell
cd "C:\Users\jaxso\projects\PC Performance booster"
git status
```

Expected: `nothing to commit, working tree clean` on `main`.

- [ ] **Sign up for a SteamGridDB API key** (free)
  - Visit: <https://www.steamgriddb.com/profile/preferences/api>
  - Sign in with Steam → click "Generate API Key" → copy the long hex string
  - This is the only third-party account v0.2 needs

- [ ] **Save the API key locally** (the app will read this file at runtime)

```powershell
$dir = Join-Path $env:LOCALAPPDATA "PrimeOSTuner"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$settings = @{ SteamGridDbApiKey = "PASTE-YOUR-KEY-HERE" } | ConvertTo-Json
$settingsPath = Join-Path $dir "settings.json"
Set-Content -Path $settingsPath -Value $settings -Encoding utf8
Get-Content $settingsPath
```

Expected: prints the JSON `{ "SteamGridDbApiKey": "PASTE-YOUR-KEY-HERE" }` — replace `PASTE-YOUR-KEY-HERE` with your real key by re-running with the actual value substituted into the command.

- [ ] **Install Steam in the VM** (so the Steam library scanner has something to find)
  - Inside the Windows 11 VM: download Steam from <https://store.steampowered.com/about/>
  - Install one small free game — recommended: "Half-Life 2: Lost Coast" or any free title — this gives the scanner real `appmanifest_*.acf` data to read.
  - **Take a new VM snapshot** named `v02-baseline` (so you can restore between game-watcher tests).

- [ ] **Confirm v0.1 still runs**

```powershell
dotnet build
dotnet test --filter "Category!=Integration&Category!=Network"
```

Expected: `Build succeeded.` and all unit tests pass.

---

## File structure (what we're adding)

Only the *new* and *modified* files compared to v0.1 are listed. Everything else from v0.1 stays put.

```
PrimeOS Tuner/
├── docs/superpowers/plans/
│   └── 2026-05-08-primeos-tuner-v0.2-game-boost-and-library.md   (this file)
├── src/
│   ├── PrimeOSTuner.UI/
│   │   ├── Controls/
│   │   │   └── GameCard.xaml                  NEW — game tile in library
│   │   ├── Dialogs/
│   │   │   └── AddGameDialog.xaml             NEW — manual add-game dialog
│   │   ├── Views/
│   │   │   ├── GameBoostView.xaml             NEW — Basic/Perf/Custom mode cards
│   │   │   ├── GameLibraryView.xaml           NEW — game grid with art
│   │   │   └── CustomModeView.xaml            NEW — tweak checklist
│   │   ├── ViewModels/
│   │   │   ├── GameBoostViewModel.cs          NEW
│   │   │   ├── GameLibraryViewModel.cs        NEW
│   │   │   ├── GameTileViewModel.cs           NEW
│   │   │   ├── CustomModeViewModel.cs         NEW
│   │   │   └── WatcherStatusViewModel.cs      NEW — sidebar bottom toggle
│   │   ├── App.xaml.cs                        MODIFY — register new services
│   │   └── MainWindow.xaml(.cs)               MODIFY — add Game Boost / Library nav
│   ├── PrimeOSTuner.Core/
│   │   ├── Tweaks/
│   │   │   ├── MouseAccelTweak.cs             NEW
│   │   │   ├── TimerResolutionTweak.cs        NEW
│   │   │   ├── GameModeTweak.cs               NEW
│   │   │   ├── HwGpuSchedulingTweak.cs        NEW
│   │   │   ├── PerAppGpuPreferenceTweak.cs    NEW
│   │   │   ├── NagleAlgorithmTweak.cs         NEW
│   │   │   ├── NetworkThrottlingIndexTweak.cs NEW
│   │   │   ├── SystemResponsivenessTweak.cs   NEW
│   │   │   └── CpuCoreParkingTweak.cs         NEW
│   │   ├── Profiles/
│   │   │   ├── ModeProfile.cs                 NEW
│   │   │   ├── BuiltInProfiles.cs             NEW
│   │   │   ├── ProfileApplier.cs              NEW
│   │   │   ├── ProfileResult.cs               NEW
│   │   │   ├── CustomProfileStore.cs          NEW
│   │   │   ├── ActiveTweaksRecord.cs          NEW
│   │   │   └── ActiveTweaksStore.cs           NEW
│   │   ├── Games/
│   │   │   ├── KnownGame.cs                   NEW
│   │   │   ├── GameRegistry.cs                NEW
│   │   │   ├── StaticGameCatalog.cs           NEW
│   │   │   ├── AddedGamesStore.cs             NEW
│   │   │   ├── GameProfileAssignment.cs       NEW
│   │   │   └── GameProfileStore.cs            NEW
│   │   ├── Lifecycle/
│   │   │   ├── IGameProcessWatcher.cs         NEW
│   │   │   ├── GameProcessWatcher.cs          NEW
│   │   │   └── ProfileLifecycleService.cs     NEW
│   ├── PrimeOSTuner.Win/
│   │   ├── Steam/
│   │   │   ├── ISteamLibraryScanner.cs        NEW
│   │   │   ├── SteamLibraryScanner.cs         NEW
│   │   │   └── SteamGame.cs                   NEW
│   │   ├── SteamGridDb/
│   │   │   ├── ISteamGridDbClient.cs          NEW
│   │   │   ├── SteamGridDbClient.cs           NEW
│   │   │   ├── SteamGridDbSettings.cs         NEW
│   │   │   ├── ArtCache.cs                    NEW
│   │   │   └── SteamGridDbDtos.cs             NEW
│   │   └── Network/
│   │       └── INetworkInterfaceClient.cs     NEW (only if needed by Nagle)
│   │       └── NetworkInterfaceClient.cs      NEW
│   └── PrimeOSTuner.Tests/
│       ├── Tweaks/                            NEW tests for each new tweak
│       ├── Profiles/                          NEW tests for ProfileApplier etc
│       ├── Games/                             NEW tests for registry / store
│       ├── Lifecycle/                         NEW tests for watcher
│       ├── Steam/                             NEW tests with VDF fixtures
│       └── SteamGridDb/                       NEW tests with mocked HttpClient
```

Storage on disk (everything under `%LOCALAPPDATA%\PrimeOSTuner\`):
- `settings.json` (already created above) — holds `SteamGridDbApiKey`
- `custom-profile.json` — user's custom tweak set
- `added-games.json` — manually-added non-Steam games
- `game-profiles.json` — per-game ModeName mapping
- `active-tweaks.json` — undo data for currently-active profile (crash recovery)
- `art-cache\{gameId}.jpg` — downloaded cover art

---

## Phase A — Universal Tweaks (9 new ITweak implementations)

### Task 1: Add the `Gameloop.Vdf` and `Microsoft.Extensions.Http` NuGet packages

**Files:**
- Modify: `src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj` (via dotnet add)
- Modify: `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj` (via dotnet add)

- [ ] **Step 1: Add VDF parser to the Win project**

```powershell
dotnet add src/PrimeOSTuner.Win package Gameloop.Vdf --version 0.6.2
```

Expected: `info : PackageReference for package 'Gameloop.Vdf' ... added.`

- [ ] **Step 2: Add `Microsoft.Extensions.Http` to the Win project (for SteamGridDB typed client)**

```powershell
dotnet add src/PrimeOSTuner.Win package Microsoft.Extensions.Http --version 8.0.1
```

- [ ] **Step 3: Build to confirm restore**

```powershell
dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "Add Gameloop.Vdf and Microsoft.Extensions.Http for v0.2"
```

---

### Task 2: MouseAccelTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/MouseAccelTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/MouseAccelTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/MouseAccelTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class MouseAccelTweakTests
{
    private const string SubKey = @"Control Panel\Mouse";

    [Fact]
    public async Task Apply_writes_three_values_and_returns_combined_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "1"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "6"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "10"));

        var tweak = new MouseAccelTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("MouseSpeed");
        result.UndoData.Should().Contain("MouseThreshold1");
        result.UndoData.Should().Contain("MouseThreshold2");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_all_three_values_are_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2")).Returns("0");

        var tweak = new MouseAccelTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_any_value_is_nonzero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed")).Returns("1");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1")).Returns("0");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2")).Returns("0");

        var tweak = new MouseAccelTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Revert_restores_all_three_backups()
    {
        var registry = new Mock<IRegistryClient>();
        var tweak = new MouseAccelTweak(registry.Object);

        // Apply first to capture undo
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseSpeed", "1"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold1", "6"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "0"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "MouseThreshold2", "10"));

        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~MouseAccelTweakTests
```

Expected: build error (`MouseAccelTweak` undefined).

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/MouseAccelTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class MouseAccelTweak : ITweak
{
    private const string SubKey = @"Control Panel\Mouse";
    private static readonly (string Name, string Value)[] Targets =
    {
        ("MouseSpeed", "0"),
        ("MouseThreshold1", "0"),
        ("MouseThreshold2", "0"),
    };

    private readonly IRegistryClient _registry;

    public string Id => "game.mouse-accel";
    public string DisplayName => "Disable mouse acceleration";
    public string Description => "Turns off Windows pointer acceleration so mouse movement maps 1:1 to pixels.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public MouseAccelTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var (name, expected) in Targets)
        {
            var current = _registry.ReadString(RegistryHive.CurrentUser, SubKey, name);
            if (current != expected)
                return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var (name, value) in Targets)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, name, value));
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
        var speed = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed") ?? "(unset)";
        var t1 = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1") ?? "(unset)";
        var t2 = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2") ?? "(unset)";
        return Task.FromResult(
            $"Will set HKCU\\{SubKey}: MouseSpeed {speed}->0, MouseThreshold1 {t1}->0, MouseThreshold2 {t2}->0.");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~MouseAccelTweakTests
```

Expected: `Passed! - 4 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add MouseAccelTweak disabling Windows pointer acceleration"
```

---

### Task 3: TimerResolutionTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/TimerResolutionTweak.cs`
- Modify: `src/PrimeOSTuner.Win/PInvoke.cs` (expose helper if not yet public)
- Create: `src/PrimeOSTuner.Win/ITimerResolutionClient.cs`
- Create: `src/PrimeOSTuner.Win/TimerResolutionClient.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/TimerResolutionTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/TimerResolutionTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TimerResolutionTweakTests
{
    [Fact]
    public async Task Apply_calls_SetResolution_with_5000_hundred_ns_units()
    {
        var client = new Mock<ITimerResolutionClient>();
        client.Setup(c => c.SetResolution(5000)).Returns(5000u);

        var tweak = new TimerResolutionTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetResolution(5000), Times.Once);
    }

    [Fact]
    public async Task Revert_calls_ClearResolution_with_5000()
    {
        var client = new Mock<ITimerResolutionClient>();
        var tweak = new TimerResolutionTweak(client.Object);

        var result = await tweak.RevertAsync("5000");

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.ClearResolution(5000), Times.Once);
    }

    [Fact]
    public async Task Probe_returns_Applied_when_current_resolution_within_tolerance()
    {
        var client = new Mock<ITimerResolutionClient>();
        client.Setup(c => c.GetCurrentResolution()).Returns(5005u);

        var tweak = new TimerResolutionTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~TimerResolutionTweakTests
```

Expected: build error.

- [ ] **Step 3: Add the Win-side interface**

`src/PrimeOSTuner.Win/ITimerResolutionClient.cs`:

```csharp
namespace PrimeOSTuner.Win;

public interface ITimerResolutionClient
{
    /// <summary>Set timer resolution in 100-ns units (5000 = 0.5 ms). Returns the actual resolution Windows granted.</summary>
    uint SetResolution(uint desiredHundredNs);
    /// <summary>Release a previously requested resolution.</summary>
    void ClearResolution(uint desiredHundredNs);
    /// <summary>Read the current effective system timer resolution in 100-ns units.</summary>
    uint GetCurrentResolution();
}
```

- [ ] **Step 4: Implement**

`src/PrimeOSTuner.Win/TimerResolutionClient.cs`:

```csharp
using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

public sealed class TimerResolutionClient : ITimerResolutionClient
{
    public uint SetResolution(uint desiredHundredNs)
    {
        PInvoke.NtSetTimerResolution(desiredHundredNs, true, out var actual);
        return actual;
    }

    public void ClearResolution(uint desiredHundredNs)
    {
        PInvoke.NtSetTimerResolution(desiredHundredNs, false, out _);
    }

    public uint GetCurrentResolution()
    {
        // Probe by setting "0 desired, false" with NtQueryTimerResolution if available; otherwise,
        // call SetResolution(0, false) which is a no-op set/release.
        NtQueryTimerResolution(out _, out _, out var current);
        return current;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint min, out uint max, out uint current);
}
```

`src/PrimeOSTuner.Core/Tweaks/TimerResolutionTweak.cs`:

```csharp
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class TimerResolutionTweak : ITweak
{
    private const uint TargetHundredNs = 5000; // 0.5 ms
    private readonly ITimerResolutionClient _client;

    public string Id => "game.timer-resolution";
    public string DisplayName => "Set system timer to 0.5 ms";
    public string Description => "Lowers the Windows scheduler tick to 0.5 ms via NtSetTimerResolution. Reduces input latency at the cost of slightly higher CPU usage.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public TimerResolutionTweak(ITimerResolutionClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var current = _client.GetCurrentResolution();
            // Anything within 10% of target counts as applied (5000 +/- 500).
            return Task.FromResult(Math.Abs((int)current - (int)TargetHundredNs) <= 500
                ? TweakState.Applied
                : TweakState.NotApplied);
        }
        catch { return Task.FromResult(TweakState.Unknown); }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        _client.SetResolution(TargetHundredNs);
        return Task.FromResult(TweakResult.Success(TargetHundredNs.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!uint.TryParse(undoData, out var hundredNs))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));
        _client.ClearResolution(hundredNs);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set system timer resolution to 0.5 ms ({TargetHundredNs} × 100 ns).");
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~TimerResolutionTweakTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add TimerResolutionTweak using NtSetTimerResolution"
```

---

### Task 4: GameModeTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/GameModeTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/GameModeTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/GameModeTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class GameModeTweakTests
{
    private const string SubKey = @"Software\Microsoft\GameBar";

    [Fact]
    public async Task Apply_writes_AllowAutoGameMode_and_AutoGameModeEnabled_to_1()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "1"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "0"));
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", "1"))
                .Returns(new RegistryBackup(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", null));

        var tweak = new GameModeTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode", "1"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled", "1"), Times.Once);
    }

    [Fact]
    public async Task Probe_returns_Applied_when_both_values_equal_one()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "AllowAutoGameMode")).Returns("1");
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, SubKey, "AutoGameModeEnabled")).Returns("1");

        var tweak = new GameModeTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~GameModeTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/GameModeTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class GameModeTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\GameBar";
    private static readonly string[] ValueNames = { "AllowAutoGameMode", "AutoGameModeEnabled" };

    private readonly IRegistryClient _registry;

    public string Id => "game.game-mode";
    public string DisplayName => "Enable Windows Game Mode";
    public string Description => "Turns on Windows Game Mode and auto-detection so Windows prioritizes the foreground game.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public GameModeTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var name in ValueNames)
            if (_registry.ReadString(RegistryHive.CurrentUser, SubKey, name) != "1")
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var name in ValueNames)
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, name, "1"));
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
        => Task.FromResult($"Will set HKCU\\{SubKey}\\AllowAutoGameMode=1 and AutoGameModeEnabled=1.");
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~GameModeTweakTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add GameModeTweak enabling Windows Game Mode auto-detection"
```

---

### Task 5: HwGpuSchedulingTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/HwGpuSchedulingTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/HwGpuSchedulingTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/HwGpuSchedulingTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class HwGpuSchedulingTweakTests
{
    private const string SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    [Fact]
    public async Task Apply_writes_HwSchMode_2_under_HKLM()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "HwSchMode", "2"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "HwSchMode", "1"));

        var tweak = new HwGpuSchedulingTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "HwSchMode", "2"), Times.Once);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new HwGpuSchedulingTweak(Mock.Of<IRegistryClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }

    [Fact]
    public async Task Probe_returns_Applied_only_when_HwSchMode_is_2()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.LocalMachine, SubKey, "HwSchMode")).Returns("2");
        var tweak = new HwGpuSchedulingTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~HwGpuSchedulingTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/HwGpuSchedulingTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class HwGpuSchedulingTweak : ITweak
{
    private const string SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    private const string ValueName = "HwSchMode";

    private readonly IRegistryClient _registry;

    public string Id => "game.hw-gpu-scheduling";
    public string DisplayName => "Enable Hardware-accelerated GPU Scheduling";
    public string Description => "Sets HwSchMode=2 so the GPU manages its own scheduling, reducing CPU overhead. Requires admin and a reboot to take effect.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public HwGpuSchedulingTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(v == "2" ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.LocalMachine, SubKey, ValueName, "2");
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
        var current = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '2'. Reboot required.");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~HwGpuSchedulingTweakTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add HwGpuSchedulingTweak setting HwSchMode=2 (HKLM, requires admin)"
```

---

### Task 6: PerAppGpuPreferenceTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/PerAppGpuPreferenceTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/PerAppGpuPreferenceTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/PerAppGpuPreferenceTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class PerAppGpuPreferenceTweakTests
{
    private const string SubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

    [Fact]
    public async Task Apply_writes_GpuPreference_2_for_each_exe_path()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, It.IsAny<string>(), "GpuPreference=2;"))
                .Returns((RegistryHive h, string s, string n, string v) => new RegistryBackup(h, s, n, null));

        var paths = new[]
        {
            @"C:\Games\Valorant\VALORANT-Win64-Shipping.exe",
            @"C:\Riot Games\League of Legends\League of Legends.exe"
        };
        var tweak = new PerAppGpuPreferenceTweak(registry.Object, paths);

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, paths[0], "GpuPreference=2;"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, paths[1], "GpuPreference=2;"), Times.Once);
    }

    [Fact]
    public async Task Apply_with_empty_path_list_succeeds_with_empty_undo()
    {
        var registry = new Mock<IRegistryClient>();
        var tweak = new PerAppGpuPreferenceTweak(registry.Object, Array.Empty<string>());

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("[]");
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~PerAppGpuPreferenceTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/PerAppGpuPreferenceTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PerAppGpuPreferenceTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueData = "GpuPreference=2;";

    private readonly IRegistryClient _registry;
    private readonly IReadOnlyList<string> _exePaths;

    public string Id => "game.per-app-gpu-pref";
    public string DisplayName => "Force high-performance GPU for installed games";
    public string Description => "Tells Windows to use the discrete GPU when launching detected game executables.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public PerAppGpuPreferenceTweak(IRegistryClient registry, IEnumerable<string> exePaths)
    {
        _registry = registry;
        _exePaths = exePaths.ToList();
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        if (_exePaths.Count == 0) return Task.FromResult(TweakState.NotApplied);
        foreach (var path in _exePaths)
            if (_registry.ReadString(RegistryHive.CurrentUser, SubKey, path) != ValueData)
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        for (int i = 0; i < _exePaths.Count; i++)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, _exePaths[i], ValueData));
            progress?.Report((i + 1) * 100 / Math.Max(1, _exePaths.Count));
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
        => Task.FromResult($"Will set HKCU\\{SubKey} entries for {_exePaths.Count} executable(s) to '{ValueData}'.");
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~PerAppGpuPreferenceTweakTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add PerAppGpuPreferenceTweak setting per-exe high-performance GPU"
```

---

### Task 7: NetworkInterfaceClient (helper for Nagle tweak)

**Files:**
- Create: `src/PrimeOSTuner.Win/Network/INetworkInterfaceClient.cs`
- Create: `src/PrimeOSTuner.Win/Network/NetworkInterfaceClient.cs`
- Create: `src/PrimeOSTuner.Tests/Win/NetworkInterfaceClientTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Win/NetworkInterfaceClientTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Win.Network;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class NetworkInterfaceClientTests
{
    [Fact]
    public void EnumerateActiveInterfaceGuids_returns_at_least_one_on_a_connected_machine()
    {
        var client = new NetworkInterfaceClient();
        var guids = client.EnumerateActiveInterfaceGuids();
        guids.Should().NotBeNull();
        // No assertion on count — a VM with no NIC could legitimately return zero.
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~NetworkInterfaceClientTests
```

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Win/Network/INetworkInterfaceClient.cs`:

```csharp
namespace PrimeOSTuner.Win.Network;

public interface INetworkInterfaceClient
{
    /// <summary>Enumerate operational network adapters' interface GUIDs (the registry-key-style strings used under Tcpip\Parameters\Interfaces).</summary>
    IReadOnlyList<string> EnumerateActiveInterfaceGuids();
}
```

- [ ] **Step 4: Implement client**

`src/PrimeOSTuner.Win/Network/NetworkInterfaceClient.cs`:

```csharp
using System.Net.NetworkInformation;

namespace PrimeOSTuner.Win.Network;

public sealed class NetworkInterfaceClient : INetworkInterfaceClient
{
    public IReadOnlyList<string> EnumerateActiveInterfaceGuids()
    {
        var result = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            // The .Id property is the "{...}" GUID string used in the registry path.
            if (!string.IsNullOrWhiteSpace(nic.Id)) result.Add(nic.Id);
        }
        return result;
    }
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~NetworkInterfaceClientTests
```

Expected: `Passed! - 1 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add NetworkInterfaceClient enumerating active NIC GUIDs"
```

---

### Task 8: NagleAlgorithmTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/NagleAlgorithmTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/NagleAlgorithmTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/NagleAlgorithmTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class NagleAlgorithmTweakTests
{
    [Fact]
    public async Task Apply_writes_TcpAckFrequency_and_TCPNoDelay_for_each_interface()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(
                RegistryHive.LocalMachine, It.IsAny<string>(), It.IsAny<string>(), "1"))
            .Returns((RegistryHive h, string s, string n, string v) => new RegistryBackup(h, s, n, null));

        var nics = new Mock<INetworkInterfaceClient>();
        nics.Setup(n => n.EnumerateActiveInterfaceGuids())
            .Returns(new[] { "{AAAAAAAA-1111-2222-3333-444444444444}" });

        var tweak = new NagleAlgorithmTweak(registry.Object, nics.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        var expectedKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AAAAAAAA-1111-2222-3333-444444444444}";
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, expectedKey, "TcpAckFrequency", "1"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, expectedKey, "TCPNoDelay", "1"), Times.Once);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new NagleAlgorithmTweak(Mock.Of<IRegistryClient>(), Mock.Of<INetworkInterfaceClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~NagleAlgorithmTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/NagleAlgorithmTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class NagleAlgorithmTweak : ITweak
{
    private const string BaseKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private static readonly string[] ValueNames = { "TcpAckFrequency", "TCPNoDelay" };

    private readonly IRegistryClient _registry;
    private readonly INetworkInterfaceClient _nics;

    public string Id => "game.nagle-algorithm";
    public string DisplayName => "Disable Nagle's Algorithm on active NICs";
    public string Description => "Sets TcpAckFrequency=1 and TCPNoDelay=1 on all active network adapters, so small packets are sent immediately. Lower latency for online games.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public NagleAlgorithmTweak(IRegistryClient registry, INetworkInterfaceClient nics)
    {
        _registry = registry;
        _nics = nics;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var guids = _nics.EnumerateActiveInterfaceGuids();
        if (guids.Count == 0) return Task.FromResult(TweakState.Unknown);
        foreach (var guid in guids)
        {
            var key = $@"{BaseKey}\{guid}";
            foreach (var name in ValueNames)
                if (_registry.ReadString(RegistryHive.LocalMachine, key, name) != "1")
                    return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        var guids = _nics.EnumerateActiveInterfaceGuids();
        foreach (var guid in guids)
        {
            var key = $@"{BaseKey}\{guid}";
            foreach (var name in ValueNames)
                backups.Add(_registry.WriteString(RegistryHive.LocalMachine, key, name, "1"));
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
        var guids = _nics.EnumerateActiveInterfaceGuids();
        return Task.FromResult($"Will set TcpAckFrequency=1 and TCPNoDelay=1 on {guids.Count} active NIC(s).");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~NagleAlgorithmTweakTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add NagleAlgorithmTweak disabling Nagle on active NICs"
```

---

### Task 9: NetworkThrottlingIndexTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/NetworkThrottlingIndexTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/NetworkThrottlingIndexTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/NetworkThrottlingIndexTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class NetworkThrottlingIndexTweakTests
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    [Fact]
    public async Task Apply_writes_NetworkThrottlingIndex_to_max_uint()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0xffffffff"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0x0000000a"));

        var tweak = new NetworkThrottlingIndexTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "NetworkThrottlingIndex", "0xffffffff"), Times.Once);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new NetworkThrottlingIndexTweak(Mock.Of<IRegistryClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~NetworkThrottlingIndexTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/NetworkThrottlingIndexTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class NetworkThrottlingIndexTweak : ITweak
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string ValueName = "NetworkThrottlingIndex";
    private const string TargetValue = "0xffffffff"; // disables throttling

    private readonly IRegistryClient _registry;

    public string Id => "game.network-throttling";
    public string DisplayName => "Disable network throttling";
    public string Description => "Removes the Windows multimedia network throttling cap so games can use full network bandwidth at all times.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public NetworkThrottlingIndexTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(string.Equals(v, TargetValue, StringComparison.OrdinalIgnoreCase)
            ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.LocalMachine, SubKey, ValueName, TargetValue);
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
        var current = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '{TargetValue}'.");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~NetworkThrottlingIndexTweakTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add NetworkThrottlingIndexTweak setting NetworkThrottlingIndex=0xffffffff"
```

---

### Task 10: SystemResponsivenessTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/SystemResponsivenessTweak.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/SystemResponsivenessTweakTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/SystemResponsivenessTweakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class SystemResponsivenessTweakTests
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    [Fact]
    public async Task Apply_writes_SystemResponsiveness_to_zero()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "0"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "20"));

        var tweak = new SystemResponsivenessTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "SystemResponsiveness", "0"), Times.Once);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~SystemResponsivenessTweakTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Tweaks/SystemResponsivenessTweak.cs`:

```csharp
using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class SystemResponsivenessTweak : ITweak
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string ValueName = "SystemResponsiveness";
    private const string TargetValue = "0";

    private readonly IRegistryClient _registry;

    public string Id => "game.system-responsiveness";
    public string DisplayName => "Maximize multimedia thread priority";
    public string Description => "Sets SystemResponsiveness=0 so multimedia threads (like games) can fully saturate the CPU instead of yielding 20% to background tasks.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public SystemResponsivenessTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(v == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.LocalMachine, SubKey, ValueName, TargetValue);
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
        var current = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '{TargetValue}'.");
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~SystemResponsivenessTweakTests
```

Expected: `Passed! - 1 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add SystemResponsivenessTweak setting SystemResponsiveness=0"
```

---

### Task 11: CpuCoreParkingTweak

**Files:**
- Create: `src/PrimeOSTuner.Core/Tweaks/CpuCoreParkingTweak.cs`
- Modify: `src/PrimeOSTuner.Win/IPowerPlanClient.cs` — add `SetActiveValueIndex` method
- Modify: `src/PrimeOSTuner.Win/PowerPlanClient.cs`
- Create: `src/PrimeOSTuner.Tests/Tweaks/CpuCoreParkingTweakTests.cs`

- [ ] **Step 1: Add the new method to `IPowerPlanClient`** (append into the existing interface)

```csharp
namespace PrimeOSTuner.Win;

public sealed record PowerPlan(Guid Guid, string Name);

public interface IPowerPlanClient
{
    IReadOnlyList<PowerPlan> ListPlans();
    PowerPlan GetActivePlan();
    void SetActivePlan(Guid planGuid);
    Guid EnsureUltimatePerformancePlan();
    /// <summary>Sets a powercfg value index on the active scheme (AC). Subgroup and setting are GUIDs or alias names like SUB_PROCESSOR / CPMINCORES.</summary>
    void SetActiveAcValueIndex(string subgroup, string setting, int value);
    /// <summary>Reads the AC index for a setting, or null if powercfg cannot return it.</summary>
    int? GetActiveAcValueIndex(string subgroup, string setting);
}
```

- [ ] **Step 2: Update `PowerPlanClient` to implement the new methods** — append to the existing class:

```csharp
public void SetActiveAcValueIndex(string subgroup, string setting, int value)
{
    RunPowerCfg($"/setacvalueindex SCHEME_CURRENT {subgroup} {setting} {value}");
    RunPowerCfg("/setactive SCHEME_CURRENT"); // reapply so changes take effect
}

public int? GetActiveAcValueIndex(string subgroup, string setting)
{
    try
    {
        var output = RunPowerCfg($"/query SCHEME_CURRENT {subgroup} {setting}");
        var rx = new System.Text.RegularExpressions.Regex(@"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
        var m = rx.Match(output);
        if (!m.Success) return null;
        return int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
    }
    catch { return null; }
}
```

- [ ] **Step 3: Write the failing test**

`src/PrimeOSTuner.Tests/Tweaks/CpuCoreParkingTweakTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class CpuCoreParkingTweakTests
{
    [Fact]
    public async Task Apply_calls_setacvalueindex_with_SUB_PROCESSOR_CPMINCORES_100()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES")).Returns(0);

        var tweak = new CpuCoreParkingTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 100), Times.Once);
        result.UndoData.Should().Contain("0");
    }

    [Fact]
    public async Task Revert_restores_previous_index()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        var result = await tweak.RevertAsync("25");

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 25), Times.Once);
    }
}
```

- [ ] **Step 4: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~CpuCoreParkingTweakTests
```

- [ ] **Step 5: Implement**

`src/PrimeOSTuner.Core/Tweaks/CpuCoreParkingTweak.cs`:

```csharp
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CpuCoreParkingTweak : ITweak
{
    private const string Subgroup = "SUB_PROCESSOR";
    private const string Setting = "CPMINCORES";
    private const int TargetValue = 100;

    private readonly IPowerPlanClient _client;

    public string Id => "game.cpu-core-parking";
    public string DisplayName => "Disable CPU core parking";
    public string Description => "Forces all CPU cores to remain unparked (Min Cores = 100%) so games can use every core under load instantly.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public CpuCoreParkingTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveAcValueIndex(Subgroup, Setting);
        if (current is null) return Task.FromResult(TweakState.Unknown);
        return Task.FromResult(current.Value == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var previous = _client.GetActiveAcValueIndex(Subgroup, Setting) ?? 0;
        _client.SetActiveAcValueIndex(Subgroup, Setting, TargetValue);
        return Task.FromResult(TweakResult.Success(previous.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!int.TryParse(undoData, out var prev))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));
        _client.SetActiveAcValueIndex(Subgroup, Setting, prev);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveAcValueIndex(Subgroup, Setting);
        return Task.FromResult($"Will run powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100. Current value: {current?.ToString() ?? "unknown"}.");
    }
}
```

- [ ] **Step 6: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~CpuCoreParkingTweakTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 7: Commit**

```powershell
git add .
git commit -m "Add CpuCoreParkingTweak setting CPMINCORES=100 via powercfg"
```

---

## Phase B — Mode Profile System

### Task 12: ModeProfile record

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/ModeProfile.cs`
- Create: `src/PrimeOSTuner.Tests/Profiles/ModeProfileTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Profiles/ModeProfileTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ModeProfileTests
{
    [Fact]
    public void ModeProfile_round_trips_through_json()
    {
        var profile = new ModeProfile(
            Id: "basic",
            DisplayName: "Basic Mode",
            Description: "Light gaming preset",
            TweakIds: new[] { "game.game-mode", "game.mouse-accel" });

        var json = JsonSerializer.Serialize(profile);
        var round = JsonSerializer.Deserialize<ModeProfile>(json);

        round.Should().NotBeNull();
        round!.Id.Should().Be("basic");
        round.TweakIds.Should().BeEquivalentTo(new[] { "game.game-mode", "game.mouse-accel" });
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ModeProfileTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Profiles/ModeProfile.cs`:

```csharp
namespace PrimeOSTuner.Core.Profiles;

public sealed record ModeProfile(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<string> TweakIds);
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ModeProfileTests
```

Expected: `Passed! - 1 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add ModeProfile record"
```

---

### Task 13: BuiltInProfiles (Basic and Performance)

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs`
- Create: `src/PrimeOSTuner.Tests/Profiles/BuiltInProfilesTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Profiles/BuiltInProfilesTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class BuiltInProfilesTests
{
    [Fact]
    public void Basic_includes_game_mode_mouse_accel_power_plan_and_visual_effects()
    {
        BuiltInProfiles.Basic.Id.Should().Be("basic");
        BuiltInProfiles.Basic.TweakIds.Should().BeEquivalentTo(new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.visual-effects"
        });
    }

    [Fact]
    public void Performance_is_a_strict_superset_of_basic()
    {
        foreach (var t in BuiltInProfiles.Basic.TweakIds)
            BuiltInProfiles.Performance.TweakIds.Should().Contain(t);

        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.timer-resolution");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.hw-gpu-scheduling");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.nagle-algorithm");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.network-throttling");
        BuiltInProfiles.Performance.TweakIds.Should().Contain("game.system-responsiveness");
    }

    [Fact]
    public void All_returns_basic_and_performance_in_order()
    {
        BuiltInProfiles.All.Should().HaveCount(2);
        BuiltInProfiles.All[0].Id.Should().Be("basic");
        BuiltInProfiles.All[1].Id.Should().Be("performance");
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~BuiltInProfilesTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Profiles/BuiltInProfiles.cs`:

```csharp
namespace PrimeOSTuner.Core.Profiles;

public static class BuiltInProfiles
{
    public static readonly ModeProfile Basic = new(
        Id: "basic",
        DisplayName: "Basic Mode",
        Description: "Lightweight gaming preset: enables Game Mode, disables mouse acceleration, switches to high performance power plan, optimizes visual effects. Safe and reversible on every PC.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.visual-effects"
        });

    public static readonly ModeProfile Performance = new(
        Id: "performance",
        DisplayName: "Performance Mode",
        Description: "Maximum gaming preset: everything in Basic, plus 0.5 ms timer resolution, hardware GPU scheduling, Nagle's algorithm disabled, network throttling removed, multimedia thread responsiveness maxed. Some tweaks require admin and a reboot.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.visual-effects",
            "game.timer-resolution",
            "game.hw-gpu-scheduling",
            "game.nagle-algorithm",
            "game.network-throttling",
            "game.system-responsiveness",
            "game.per-app-gpu-pref"
        });

    public static readonly IReadOnlyList<ModeProfile> All = new[] { Basic, Performance };
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~BuiltInProfilesTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add BuiltInProfiles defining Basic and Performance modes"
```

---

### Task 14: ProfileResult record

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/ProfileResult.cs`

- [ ] **Step 1: Implement directly** — pure data type, no test required.

`src/PrimeOSTuner.Core/Profiles/ProfileResult.cs`:

```csharp
namespace PrimeOSTuner.Core.Profiles;

public sealed record ProfileTweakOutcome(
    string TweakId,
    bool Succeeded,
    string? UndoData,
    string? Error);

public sealed record ProfileResult(
    string ProfileId,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ProfileTweakOutcome> Outcomes)
{
    public bool AllSucceeded => FailureCount == 0;
}
```

- [ ] **Step 2: Build to confirm**

```powershell
dotnet build src/PrimeOSTuner.Core
```

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add ProfileResult and ProfileTweakOutcome records"
```

---

### Task 15: ProfileApplier — runs profiles, records history

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/ProfileApplier.cs`
- Create: `src/PrimeOSTuner.Tests/Profiles/ProfileApplierTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/PrimeOSTuner.Tests/Profiles/ProfileApplierTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ProfileApplierTests
{
    private static Mock<ITweak> StubTweak(string id, bool succeeds = true)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(succeeds ? TweakResult.Success("undo-" + id) : TweakResult.Failure("nope"));
        m.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .ReturnsAsync(TweakResult.Success());
        return m;
    }

    [Fact]
    public async Task ApplyAsync_runs_each_resolved_tweak_in_order_and_records_history()
    {
        var a = StubTweak("a");
        var b = StubTweak("b");
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var history = new TweakHistory(historyPath);

        var applier = new ProfileApplier(new[] { a.Object, b.Object }, history);
        var profile = new ModeProfile("p", "P", "P", new[] { "a", "b" });

        var result = await applier.ApplyAsync(profile);

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
        result.Outcomes.Select(o => o.TweakId).Should().BeEquivalentTo(new[] { "a", "b" });
        (await history.LoadAsync()).Should().HaveCount(2);

        File.Delete(historyPath);
    }

    [Fact]
    public async Task ApplyAsync_skips_unknown_tweak_ids_and_records_failure()
    {
        var a = StubTweak("a");
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var applier = new ProfileApplier(new[] { a.Object }, new TweakHistory(historyPath));
        var profile = new ModeProfile("p", "P", "P", new[] { "a", "missing" });

        var result = await applier.ApplyAsync(profile);

        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);
        result.Outcomes.First(o => o.TweakId == "missing").Error.Should().Contain("not registered");

        File.Delete(historyPath);
    }

    [Fact]
    public async Task RevertAsync_calls_revert_on_each_outcome_in_reverse_order()
    {
        var a = StubTweak("a");
        var b = StubTweak("b");
        var calls = new List<string>();
        a.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .Callback<string, CancellationToken>((_, _) => calls.Add("a"))
            .ReturnsAsync(TweakResult.Success());
        b.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .Callback<string, CancellationToken>((_, _) => calls.Add("b"))
            .ReturnsAsync(TweakResult.Success());

        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid()}.json");
        var applier = new ProfileApplier(new[] { a.Object, b.Object }, new TweakHistory(historyPath));

        var outcomes = new[]
        {
            new ProfileTweakOutcome("a", true, "undo-a", null),
            new ProfileTweakOutcome("b", true, "undo-b", null),
        };
        await applier.RevertAsync(outcomes);

        calls.Should().Equal(new[] { "b", "a" });

        File.Delete(historyPath);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ProfileApplierTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Profiles/ProfileApplier.cs`:

```csharp
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Profiles;

public sealed class ProfileApplier
{
    private readonly Dictionary<string, ITweak> _tweaks;
    private readonly TweakHistory _history;

    public ProfileApplier(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        _tweaks = tweaks.ToDictionary(t => t.Id);
        _history = history;
    }

    public async Task<ProfileResult> ApplyAsync(
        ModeProfile profile,
        IProgress<(int Done, int Total, string CurrentName)>? progress = null,
        CancellationToken ct = default)
    {
        var outcomes = new List<ProfileTweakOutcome>();
        int success = 0, failure = 0;

        for (int i = 0; i < profile.TweakIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var id = profile.TweakIds[i];

            if (!_tweaks.TryGetValue(id, out var tweak))
            {
                failure++;
                outcomes.Add(new ProfileTweakOutcome(id, false, null, $"Tweak '{id}' not registered"));
                continue;
            }

            progress?.Report((i, profile.TweakIds.Count, tweak.DisplayName));

            try
            {
                var r = await tweak.ApplyAsync(null, ct);
                if (r.Succeeded)
                {
                    success++;
                    outcomes.Add(new ProfileTweakOutcome(id, true, r.UndoData, null));
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), id, tweak.DisplayName, DateTime.UtcNow, r.UndoData, false));
                }
                else
                {
                    failure++;
                    outcomes.Add(new ProfileTweakOutcome(id, false, null, r.Error));
                }
            }
            catch (Exception ex)
            {
                failure++;
                outcomes.Add(new ProfileTweakOutcome(id, false, null, ex.Message));
            }
        }

        progress?.Report((profile.TweakIds.Count, profile.TweakIds.Count, "Done"));
        return new ProfileResult(profile.Id, success, failure, outcomes);
    }

    public async Task RevertAsync(IReadOnlyList<ProfileTweakOutcome> outcomes, CancellationToken ct = default)
    {
        for (int i = outcomes.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            var o = outcomes[i];
            if (!o.Succeeded || o.UndoData is null) continue;
            if (!_tweaks.TryGetValue(o.TweakId, out var tweak)) continue;

            try
            {
                await tweak.RevertAsync(o.UndoData, ct);
            }
            catch
            {
                // Continue reverting the rest; partial recovery is better than none.
            }
        }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ProfileApplierTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add ProfileApplier running tweak IDs and recording history"
```

---

### Task 16: CustomProfileStore (JSON persistence)

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/CustomProfileStore.cs`
- Create: `src/PrimeOSTuner.Tests/Profiles/CustomProfileStoreTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Profiles/CustomProfileStoreTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class CustomProfileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_default_empty_profile_when_file_missing()
    {
        var store = new CustomProfileStore(_path);
        var profile = await store.LoadAsync();
        profile.Id.Should().Be("custom");
        profile.TweakIds.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_tweak_ids()
    {
        var store = new CustomProfileStore(_path);
        await store.SaveAsync(new[] { "game.game-mode", "game.timer-resolution" });

        var loaded = await store.LoadAsync();

        loaded.TweakIds.Should().BeEquivalentTo(new[] { "game.game-mode", "game.timer-resolution" });
    }

    [Fact]
    public async Task SaveAsync_creates_parent_directory()
    {
        var nested = Path.Combine(Path.GetTempPath(), $"primeos-{Guid.NewGuid()}", "sub", "custom.json");
        var store = new CustomProfileStore(nested);
        try
        {
            await store.SaveAsync(new[] { "x" });
            File.Exists(nested).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(nested)!))
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(nested)!)!, true);
        }
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~CustomProfileStoreTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Profiles/CustomProfileStore.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Profiles;

public sealed class CustomProfileStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public CustomProfileStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "custom-profile.json");

    public async Task<ModeProfile> LoadAsync()
    {
        if (!File.Exists(_path))
            return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", Array.Empty<string>());

        var json = await File.ReadAllTextAsync(_path);
        var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        return new ModeProfile("custom", "Custom Mode", "Your hand-picked tweak set.", ids);
    }

    public async Task SaveAsync(IEnumerable<string> tweakIds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(tweakIds.ToList(), JsonOpts);
        await File.WriteAllTextAsync(_path, json);
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~CustomProfileStoreTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add CustomProfileStore for JSON-backed user-picked tweak set"
```

---

### Task 17: ActiveTweaksStore (crash-safe undo persistence)

**Files:**
- Create: `src/PrimeOSTuner.Core/Profiles/ActiveTweaksRecord.cs`
- Create: `src/PrimeOSTuner.Core/Profiles/ActiveTweaksStore.cs`
- Create: `src/PrimeOSTuner.Tests/Profiles/ActiveTweaksStoreTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Profiles/ActiveTweaksStoreTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ActiveTweaksStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"active-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_null_when_file_missing()
    {
        var store = new ActiveTweaksStore(_path);
        (await store.LoadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Save_then_Load_round_trips()
    {
        var store = new ActiveTweaksStore(_path);
        var record = new ActiveTweaksRecord(
            "valorant",
            "performance",
            DateTime.UtcNow,
            new[]
            {
                new ProfileTweakOutcome("game.game-mode", true, "undo-1", null),
                new ProfileTweakOutcome("game.mouse-accel", true, "undo-2", null),
            });

        await store.SaveAsync(record);
        var loaded = await store.LoadAsync();

        loaded.Should().NotBeNull();
        loaded!.GameId.Should().Be("valorant");
        loaded.Outcomes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Clear_removes_the_file()
    {
        var store = new ActiveTweaksStore(_path);
        await store.SaveAsync(new ActiveTweaksRecord("g", "p", DateTime.UtcNow, Array.Empty<ProfileTweakOutcome>()));
        File.Exists(_path).Should().BeTrue();

        await store.ClearAsync();

        File.Exists(_path).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ActiveTweaksStoreTests
```

- [ ] **Step 3: Implement record**

`src/PrimeOSTuner.Core/Profiles/ActiveTweaksRecord.cs`:

```csharp
namespace PrimeOSTuner.Core.Profiles;

public sealed record ActiveTweaksRecord(
    string GameId,
    string ProfileId,
    DateTime AppliedAtUtc,
    IReadOnlyList<ProfileTweakOutcome> Outcomes);
```

- [ ] **Step 4: Implement store**

`src/PrimeOSTuner.Core/Profiles/ActiveTweaksStore.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Profiles;

public sealed class ActiveTweaksStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ActiveTweaksStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "active-tweaks.json");

    public async Task<ActiveTweaksRecord?> LoadAsync()
    {
        if (!File.Exists(_path)) return null;
        var json = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<ActiveTweaksRecord>(json);
    }

    public async Task SaveAsync(ActiveTweaksRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(record, JsonOpts);
        await File.WriteAllTextAsync(_path, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ActiveTweaksStoreTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add ActiveTweaksStore for crash-safe undo persistence"
```

---


## Phase C — Steam Library Scanner

### Task 18: SteamGame record

**Files:**
- Create: `src/PrimeOSTuner.Win/Steam/SteamGame.cs`

- [ ] **Step 1: Implement directly** — pure record, no test needed.

`src/PrimeOSTuner.Win/Steam/SteamGame.cs`:

```csharp
namespace PrimeOSTuner.Win.Steam;

public sealed record SteamGame(
    string AppId,
    string Name,
    string InstallDir,
    string LibraryPath,
    string? PrimaryExecutablePath);
```

- [ ] **Step 2: Build to confirm**

```powershell
dotnet build src/PrimeOSTuner.Win
```

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add SteamGame record"
```

---

### Task 19: ISteamLibraryScanner + SteamLibraryScanner with VDF parsing

**Files:**
- Create: `src/PrimeOSTuner.Win/Steam/ISteamLibraryScanner.cs`
- Create: `src/PrimeOSTuner.Win/Steam/SteamLibraryScanner.cs`
- Create: `src/PrimeOSTuner.Tests/Steam/SteamLibraryScannerTests.cs`
- Create: `src/PrimeOSTuner.Tests/Steam/Fixtures/libraryfolders.vdf`
- Create: `src/PrimeOSTuner.Tests/Steam/Fixtures/appmanifest_440.acf`

- [ ] **Step 1: Create the test fixture VDF** at `src/PrimeOSTuner.Tests/Steam/Fixtures/libraryfolders.vdf`

```
"libraryfolders"
{
    "0"
    {
        "path"        "C:\\Program Files (x86)\\Steam"
        "label"       ""
        "contentid"   "1234567890"
        "totalsize"   "0"
        "apps"
        {
            "440"     "1024000"
        }
    }
    "1"
    {
        "path"        "D:\\SteamLibrary"
        "label"       ""
        "contentid"   "9876543210"
        "totalsize"   "500000000000"
        "apps"
        {
            "730"     "20000000"
        }
    }
}
```

- [ ] **Step 2: Create the test fixture ACF** at `src/PrimeOSTuner.Tests/Steam/Fixtures/appmanifest_440.acf`

```
"AppState"
{
    "appid"          "440"
    "name"           "Team Fortress 2"
    "installdir"     "Team Fortress 2"
    "StateFlags"     "4"
    "LastUpdated"    "1700000000"
    "SizeOnDisk"     "20000000000"
}
```

- [ ] **Step 3: Mark fixtures as content in test csproj** — modify `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj` to copy fixtures to output. Add inside the `<Project>` element after the existing `<PropertyGroup>`:

```xml
<ItemGroup>
    <None Update="Steam\Fixtures\libraryfolders.vdf">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="Steam\Fixtures\appmanifest_440.acf">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

- [ ] **Step 4: Write the failing test**

`src/PrimeOSTuner.Tests/Steam/SteamLibraryScannerTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.Steam;

public class SteamLibraryScannerTests
{
    [Fact]
    public void ParseLibraryFolders_returns_each_path_in_file()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "libraryfolders.vdf");
        File.Exists(fixturePath).Should().BeTrue("fixture must be copied to output");

        var paths = SteamLibraryScanner.ParseLibraryFolders(fixturePath);

        paths.Should().Contain(@"C:\Program Files (x86)\Steam");
        paths.Should().Contain(@"D:\SteamLibrary");
    }

    [Fact]
    public void ParseAppManifest_extracts_appid_name_and_installdir()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "appmanifest_440.acf");

        var game = SteamLibraryScanner.ParseAppManifest(fixturePath, libraryPath: @"C:\Program Files (x86)\Steam");

        game.Should().NotBeNull();
        game!.AppId.Should().Be("440");
        game.Name.Should().Be("Team Fortress 2");
        game.InstallDir.Should().Be("Team Fortress 2");
    }

    [Fact]
    public void ParseAppManifest_returns_null_when_file_missing()
    {
        var game = SteamLibraryScanner.ParseAppManifest(@"C:\does\not\exist.acf", libraryPath: @"C:\");
        game.Should().BeNull();
    }
}
```

- [ ] **Step 5: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~SteamLibraryScannerTests
```

Expected: build error (`SteamLibraryScanner` undefined).

- [ ] **Step 6: Implement interface**

`src/PrimeOSTuner.Win/Steam/ISteamLibraryScanner.cs`:

```csharp
namespace PrimeOSTuner.Win.Steam;

public interface ISteamLibraryScanner
{
    /// <summary>Returns all installed Steam games found across all configured Steam libraries on this machine. Returns empty list if Steam is not installed.</summary>
    IReadOnlyList<SteamGame> ScanInstalledGames();
}
```

- [ ] **Step 7: Implement scanner**

`src/PrimeOSTuner.Win/Steam/SteamLibraryScanner.cs`:

```csharp
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace PrimeOSTuner.Win.Steam;

public sealed class SteamLibraryScanner : ISteamLibraryScanner
{
    public IReadOnlyList<SteamGame> ScanInstalledGames()
    {
        var steamRoot = ReadSteamPath();
        if (steamRoot is null || !Directory.Exists(steamRoot))
            return Array.Empty<SteamGame>();

        var libraryFoldersVdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        var libraryPaths = ParseLibraryFolders(libraryFoldersVdf);
        if (libraryPaths.Count == 0) libraryPaths = new[] { steamRoot };

        var games = new List<SteamGame>();
        foreach (var libRoot in libraryPaths)
        {
            var steamapps = Path.Combine(libRoot, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                var g = ParseAppManifest(manifest, libRoot);
                if (g is not null) games.Add(g);
            }
        }
        return games;
    }

    public static IReadOnlyList<string> ParseLibraryFolders(string libraryFoldersVdfPath)
    {
        if (!File.Exists(libraryFoldersVdfPath)) return Array.Empty<string>();
        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(libraryFoldersVdfPath));
            var result = new List<string>();
            foreach (var child in (VObject)root.Value)
            {
                if (child.Value is VObject obj && obj.TryGetValue("path", out var pathToken))
                {
                    var p = pathToken.ToString().Replace(@"\\", @"\");
                    if (!string.IsNullOrWhiteSpace(p)) result.Add(p);
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static SteamGame? ParseAppManifest(string acfPath, string libraryPath)
    {
        if (!File.Exists(acfPath)) return null;
        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(acfPath));
            var state = (VObject)root.Value;
            var appId = state["appid"]?.ToString() ?? "";
            var name = state["name"]?.ToString() ?? "";
            var installDir = state["installdir"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name)) return null;

            string? exePath = null;
            var commonInstall = Path.Combine(libraryPath, "steamapps", "common", installDir);
            if (Directory.Exists(commonInstall))
            {
                exePath = Directory.EnumerateFiles(commonInstall, "*.exe", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }

            return new SteamGame(appId, name, installDir, libraryPath, exePath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static class VObjectExtensions { }
}

internal static class VObjectAccess
{
    public static bool TryGetValue(this VObject obj, string key, out VToken? token)
    {
        if (obj.ContainsKey(key))
        {
            token = obj[key];
            return true;
        }
        token = null;
        return false;
    }
}
```

- [ ] **Step 8: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~SteamLibraryScannerTests
```

Expected: `Passed! - 3 passed`. If `Gameloop.Vdf` indexer differs from this code, adjust the `state["appid"]` calls to use `state.Value<string>("appid")` (the API has had two release lines) — confirmed at the build step.

- [ ] **Step 9: Commit**

```powershell
git add .
git commit -m "Add SteamLibraryScanner parsing libraryfolders.vdf and appmanifest ACFs"
```

---


## Phase D — SteamGridDB API Client

### Task 20: SteamGridDbSettings

**Files:**
- Create: `src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbSettings.cs`
- Create: `src/PrimeOSTuner.Tests/SteamGridDb/SteamGridDbSettingsTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/SteamGridDb/SteamGridDbSettingsTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class SteamGridDbSettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public void Load_returns_empty_settings_when_file_missing()
    {
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().BeNull();
    }

    [Fact]
    public void Load_reads_api_key_from_file()
    {
        File.WriteAllText(_path, "{\"SteamGridDbApiKey\":\"abc123\"}");
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().Be("abc123");
    }

    [Fact]
    public void Load_returns_empty_settings_when_file_is_invalid_json()
    {
        File.WriteAllText(_path, "{this is not json");
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~SteamGridDbSettingsTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbSettings.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SteamGridDbSettings
{
    public string? SteamGridDbApiKey { get; set; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "settings.json");

    public static SteamGridDbSettings Load(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return new SteamGridDbSettings();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SteamGridDbSettings>(json) ?? new SteamGridDbSettings();
        }
        catch
        {
            return new SteamGridDbSettings();
        }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~SteamGridDbSettingsTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add SteamGridDbSettings reading API key from settings.json"
```

---

### Task 21: SteamGridDb DTOs

**Files:**
- Create: `src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbDtos.cs`

- [ ] **Step 1: Implement directly** — the API response shapes documented at <https://www.steamgriddb.com/api/v2>.

`src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbDtos.cs`:

```csharp
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SgdbResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")]    public T? Data { get; set; }
    [JsonPropertyName("errors")]  public List<string>? Errors { get; set; }
}

public sealed class SgdbGameRef
{
    [JsonPropertyName("id")]   public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class SgdbGrid
{
    [JsonPropertyName("id")]     public long Id { get; set; }
    [JsonPropertyName("url")]    public string Url { get; set; } = "";
    [JsonPropertyName("thumb")]  public string Thumb { get; set; } = "";
    [JsonPropertyName("width")]  public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

/// <summary>What the rest of the app sees: a resolved cover-art URL plus minimal metadata. Null URL = no art available.</summary>
public sealed record CoverArt(long? GameId, string GameName, string? Url, string? Thumb);
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/PrimeOSTuner.Win
```

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add SteamGridDb DTO classes and CoverArt record"
```

---

### Task 22: ISteamGridDbClient + SteamGridDbClient

**Files:**
- Create: `src/PrimeOSTuner.Win/SteamGridDb/ISteamGridDbClient.cs`
- Create: `src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbClient.cs`
- Create: `src/PrimeOSTuner.Tests/SteamGridDb/SteamGridDbClientTests.cs`

- [ ] **Step 1: Write the failing test using a fake `HttpMessageHandler`**

`src/PrimeOSTuner.Tests/SteamGridDb/SteamGridDbClientTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class SteamGridDbClientTests
{
    private static HttpClient FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new HttpClient(new FakeHandler(handler))
        {
            BaseAddress = new Uri("https://www.steamgriddb.com/")
        };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> h) { _handler = h; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(_handler(req));
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_no_key_status_when_api_key_missing()
    {
        var http = FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = null };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");

        art.Url.Should().BeNull();
        client.HasApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_url_when_api_returns_grid()
    {
        var calls = new List<string>();
        var http = FakeHttp(req =>
        {
            calls.Add(req.RequestUri!.PathAndQuery);
            string body = req.RequestUri!.AbsolutePath.Contains("/games/steam/")
                ? "{\"success\":true,\"data\":{\"id\":99,\"name\":\"Team Fortress 2\"}}"
                : "{\"success\":true,\"data\":[{\"id\":1,\"url\":\"https://cdn.example/cover.jpg\",\"thumb\":\"https://cdn.example/thumb.jpg\",\"width\":600,\"height\":900}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");

        art.Url.Should().Be("https://cdn.example/cover.jpg");
        art.GameId.Should().Be(99);
        calls.Should().HaveCount(2);
        calls[0].Should().Contain("/games/steam/440");
        calls[1].Should().Contain("/grids/game/99");
    }

    [Fact]
    public async Task SearchAsync_uses_autocomplete_endpoint()
    {
        var calls = new List<string>();
        var http = FakeHttp(req =>
        {
            calls.Add(req.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Valorant\"}]}",
                    Encoding.UTF8, "application/json")
            };
        });
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var hits = await client.SearchAsync("valorant");

        hits.Should().ContainSingle(h => h.Name == "Valorant" && h.Id == 42);
        calls.Single().Should().Contain("/search/autocomplete/valorant");
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_null_url_when_api_call_fails()
    {
        var http = FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");
        art.Url.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~SteamGridDbClientTests
```

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Win/SteamGridDb/ISteamGridDbClient.cs`:

```csharp
namespace PrimeOSTuner.Win.SteamGridDb;

public interface ISteamGridDbClient
{
    bool HasApiKey { get; }
    Task<CoverArt> GetCoverByAppIdAsync(string steamAppId, CancellationToken ct = default);
    Task<IReadOnlyList<SgdbGameRef>> SearchAsync(string query, CancellationToken ct = default);
    Task<CoverArt> GetCoverByGameIdAsync(long sgdbGameId, string fallbackName, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement client**

`src/PrimeOSTuner.Win/SteamGridDb/SteamGridDbClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class SteamGridDbClient : ISteamGridDbClient
{
    private const string BaseUri = "https://www.steamgriddb.com";
    private readonly HttpClient _http;
    private readonly SteamGridDbSettings _settings;

    public SteamGridDbClient(HttpClient http, SteamGridDbSettings settings)
    {
        _http = http;
        _settings = settings;
        if (_http.BaseAddress is null) _http.BaseAddress = new Uri(BaseUri);
        if (HasApiKey)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.SteamGridDbApiKey);
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_settings.SteamGridDbApiKey);

    public async Task<CoverArt> GetCoverByAppIdAsync(string steamAppId, CancellationToken ct = default)
    {
        if (!HasApiKey) return new CoverArt(null, "", null, null);

        try
        {
            var lookup = await _http.GetFromJsonAsync<SgdbResponse<SgdbGameRef>>(
                $"/api/v2/games/steam/{steamAppId}", ct);
            if (lookup?.Success != true || lookup.Data is null)
                return new CoverArt(null, "", null, null);

            return await GetCoverByGameIdAsync(lookup.Data.Id, lookup.Data.Name, ct);
        }
        catch
        {
            return new CoverArt(null, "", null, null);
        }
    }

    public async Task<CoverArt> GetCoverByGameIdAsync(long sgdbGameId, string fallbackName, CancellationToken ct = default)
    {
        if (!HasApiKey) return new CoverArt(sgdbGameId, fallbackName, null, null);
        try
        {
            var grids = await _http.GetFromJsonAsync<SgdbResponse<List<SgdbGrid>>>(
                $"/api/v2/grids/game/{sgdbGameId}?dimensions=600x900", ct);
            var first = grids?.Data?.FirstOrDefault();
            return new CoverArt(sgdbGameId, fallbackName, first?.Url, first?.Thumb);
        }
        catch
        {
            return new CoverArt(sgdbGameId, fallbackName, null, null);
        }
    }

    public async Task<IReadOnlyList<SgdbGameRef>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!HasApiKey) return Array.Empty<SgdbGameRef>();
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var resp = await _http.GetFromJsonAsync<SgdbResponse<List<SgdbGameRef>>>(
                $"/api/v2/search/autocomplete/{encoded}", ct);
            return resp?.Data ?? new List<SgdbGameRef>();
        }
        catch
        {
            return Array.Empty<SgdbGameRef>();
        }
    }
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~SteamGridDbClientTests
```

Expected: `Passed! - 4 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add SteamGridDbClient with graceful no-API-key fallback"
```

---

### Task 23: ArtCache (downloads-then-disk)

**Files:**
- Create: `src/PrimeOSTuner.Win/SteamGridDb/ArtCache.cs`
- Create: `src/PrimeOSTuner.Tests/SteamGridDb/ArtCacheTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/SteamGridDb/ArtCacheTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class ArtCacheTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"art-{Guid.NewGuid()}");

    public void Dispose() { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true); }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        public int CallCount { get; private set; }
        public StaticHandler(byte[] bytes) { _bytes = bytes; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_bytes)
            });
        }
    }

    [Fact]
    public async Task GetOrDownloadAsync_returns_cached_path_after_first_call()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new StaticHandler(bytes);
        var http = new HttpClient(handler);
        var cache = new ArtCache(_cacheDir, http);

        var path1 = await cache.GetOrDownloadAsync(123, "https://example/x.jpg");
        var path2 = await cache.GetOrDownloadAsync(123, "https://example/x.jpg");

        path1.Should().Be(path2);
        File.Exists(path1!).Should().BeTrue();
        handler.CallCount.Should().Be(1);
        File.ReadAllBytes(path1!).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task GetOrDownloadAsync_returns_null_when_url_is_null()
    {
        var http = new HttpClient(new StaticHandler(Array.Empty<byte>()));
        var cache = new ArtCache(_cacheDir, http);

        var path = await cache.GetOrDownloadAsync(456, null);
        path.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ArtCacheTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Win/SteamGridDb/ArtCache.cs`:

```csharp
namespace PrimeOSTuner.Win.SteamGridDb;

public sealed class ArtCache
{
    private readonly string _cacheDir;
    private readonly HttpClient _http;

    public ArtCache(string cacheDir, HttpClient http)
    {
        _cacheDir = cacheDir;
        _http = http;
        Directory.CreateDirectory(_cacheDir);
    }

    public static string DefaultDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "art-cache");

    public async Task<string?> GetOrDownloadAsync(long gameId, string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var path = Path.Combine(_cacheDir, $"{gameId}.jpg");
        if (File.Exists(path)) return path;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ArtCacheTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add ArtCache downloading once and reading from disk thereafter"
```

---


## Phase E — Game Registry (Core)

### Task 24: KnownGame record

**Files:**
- Create: `src/PrimeOSTuner.Core/Games/KnownGame.cs`

- [ ] **Step 1: Implement directly**

`src/PrimeOSTuner.Core/Games/KnownGame.cs`:

```csharp
namespace PrimeOSTuner.Core.Games;

public enum KnownGameSource
{
    Steam,
    StaticCatalog,
    UserAdded
}

public sealed record KnownGame(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ExecutableNames,
    string? SteamAppId,
    string? InstallPath,
    KnownGameSource Source);
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/PrimeOSTuner.Core
```

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add KnownGame record with source classification"
```

---

### Task 25: StaticGameCatalog

**Files:**
- Create: `src/PrimeOSTuner.Core/Games/StaticGameCatalog.cs`
- Create: `src/PrimeOSTuner.Tests/Games/StaticGameCatalogTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Games/StaticGameCatalogTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class StaticGameCatalogTests
{
    [Fact]
    public void Catalog_includes_valorant_with_correct_executable()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.valorant");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("VALORANT-Win64-Shipping.exe");
    }

    [Fact]
    public void Catalog_includes_league_of_legends()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.league-of-legends");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("League of Legends.exe");
    }

    [Fact]
    public void Catalog_includes_fortnite_epic()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.fortnite");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("FortniteClient-Win64-Shipping.exe");
    }

    [Fact]
    public void Every_entry_uses_StaticCatalog_source()
    {
        StaticGameCatalog.All.Should().OnlyContain(g => g.Source == KnownGameSource.StaticCatalog);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~StaticGameCatalogTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Games/StaticGameCatalog.cs`:

```csharp
namespace PrimeOSTuner.Core.Games;

public static class StaticGameCatalog
{
    public static readonly IReadOnlyList<KnownGame> All = new[]
    {
        new KnownGame(
            Id: "static.valorant",
            DisplayName: "VALORANT",
            ExecutableNames: new[] { "VALORANT-Win64-Shipping.exe", "VALORANT.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.league-of-legends",
            DisplayName: "League of Legends",
            ExecutableNames: new[] { "League of Legends.exe", "LeagueClient.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.fortnite",
            DisplayName: "Fortnite",
            ExecutableNames: new[] { "FortniteClient-Win64-Shipping.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.apex-legends",
            DisplayName: "Apex Legends",
            ExecutableNames: new[] { "r5apex.exe" },
            SteamAppId: "1172470",
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.overwatch",
            DisplayName: "Overwatch 2",
            ExecutableNames: new[] { "Overwatch.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
        new KnownGame(
            Id: "static.minecraft",
            DisplayName: "Minecraft",
            ExecutableNames: new[] { "Minecraft.Windows.exe", "javaw.exe" },
            SteamAppId: null,
            InstallPath: null,
            Source: KnownGameSource.StaticCatalog),
    };
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~StaticGameCatalogTests
```

Expected: `Passed! - 4 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add StaticGameCatalog with well-known non-Steam game executables"
```

---

### Task 26: AddedGamesStore (user-added games)

**Files:**
- Create: `src/PrimeOSTuner.Core/Games/AddedGamesStore.cs`
- Create: `src/PrimeOSTuner.Tests/Games/AddedGamesStoreTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Games/AddedGamesStoreTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class AddedGamesStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"added-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_empty_when_file_missing()
    {
        var store = new AddedGamesStore(_path);
        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_persists_game_with_UserAdded_source()
    {
        var store = new AddedGamesStore(_path);
        await store.AddAsync(new KnownGame(
            "user.test", "Test Game", new[] { "test.exe" }, null, @"C:\Games\Test", KnownGameSource.UserAdded));

        var loaded = await store.LoadAsync();
        loaded.Should().HaveCount(1);
        loaded[0].DisplayName.Should().Be("Test Game");
        loaded[0].Source.Should().Be(KnownGameSource.UserAdded);
    }

    [Fact]
    public async Task RemoveAsync_drops_matching_id()
    {
        var store = new AddedGamesStore(_path);
        await store.AddAsync(new KnownGame("a", "A", new[] { "a.exe" }, null, null, KnownGameSource.UserAdded));
        await store.AddAsync(new KnownGame("b", "B", new[] { "b.exe" }, null, null, KnownGameSource.UserAdded));

        await store.RemoveAsync("a");

        (await store.LoadAsync()).Select(g => g.Id).Should().BeEquivalentTo(new[] { "b" });
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~AddedGamesStoreTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Games/AddedGamesStore.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Games;

public sealed class AddedGamesStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AddedGamesStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "added-games.json");

    public async Task<IReadOnlyList<KnownGame>> LoadAsync()
    {
        if (!File.Exists(_path)) return Array.Empty<KnownGame>();
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<List<KnownGame>>(json) ?? new List<KnownGame>();
        }
        catch
        {
            return Array.Empty<KnownGame>();
        }
    }

    public async Task AddAsync(KnownGame game)
    {
        var list = (await LoadAsync()).ToList();
        list.RemoveAll(g => g.Id == game.Id);
        list.Add(game);
        await SaveAsync(list);
    }

    public async Task RemoveAsync(string id)
    {
        var list = (await LoadAsync()).Where(g => g.Id != id).ToList();
        await SaveAsync(list);
    }

    private async Task SaveAsync(List<KnownGame> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(list, JsonOpts));
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~AddedGamesStoreTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add AddedGamesStore for user-added games"
```

---

### Task 27: GameRegistry — combines Steam + static + user-added

**Files:**
- Create: `src/PrimeOSTuner.Core/Games/GameRegistry.cs`
- Create: `src/PrimeOSTuner.Tests/Games/GameRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Games/GameRegistryTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class GameRegistryTests : IDisposable
{
    private readonly string _addedPath = Path.Combine(Path.GetTempPath(), $"reg-added-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_addedPath)) File.Delete(_addedPath); }

    [Fact]
    public async Task GetAllAsync_returns_steam_static_and_user_added_combined()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("440", "Team Fortress 2", "Team Fortress 2", @"C:\Steam", @"C:\Steam\tf2.exe")
        });
        var added = new AddedGamesStore(_addedPath);
        await added.AddAsync(new KnownGame("user.x", "Custom Game", new[] { "x.exe" }, null, null, KnownGameSource.UserAdded));

        var registry = new GameRegistry(scanner.Object, added);
        var all = await registry.GetAllAsync();

        all.Should().Contain(g => g.Source == KnownGameSource.Steam && g.DisplayName == "Team Fortress 2");
        all.Should().Contain(g => g.Source == KnownGameSource.StaticCatalog && g.Id == "static.valorant");
        all.Should().Contain(g => g.Source == KnownGameSource.UserAdded && g.Id == "user.x");
    }

    [Fact]
    public async Task GetAllAsync_de_duplicates_when_static_game_is_also_in_steam()
    {
        // Apex Legends is in static catalog with SteamAppId 1172470; if scanner also returns it, only one entry results.
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(new[]
        {
            new SteamGame("1172470", "Apex Legends", "Apex Legends", @"C:\Steam", null)
        });
        var added = new AddedGamesStore(_addedPath);
        var registry = new GameRegistry(scanner.Object, added);

        var all = await registry.GetAllAsync();

        all.Where(g => g.SteamAppId == "1172470").Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~GameRegistryTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Games/GameRegistry.cs`:

```csharp
using PrimeOSTuner.Win.Steam;

namespace PrimeOSTuner.Core.Games;

public sealed class GameRegistry
{
    private readonly ISteamLibraryScanner _steam;
    private readonly AddedGamesStore _added;

    public GameRegistry(ISteamLibraryScanner steam, AddedGamesStore added)
    {
        _steam = steam;
        _added = added;
    }

    public async Task<IReadOnlyList<KnownGame>> GetAllAsync()
    {
        var result = new List<KnownGame>();
        var seenSteamIds = new HashSet<string>();

        // Steam-detected games
        foreach (var sg in _steam.ScanInstalledGames())
        {
            var exeName = sg.PrimaryExecutablePath is not null
                ? Path.GetFileName(sg.PrimaryExecutablePath)
                : null;
            var exes = exeName is null ? Array.Empty<string>() : new[] { exeName };
            result.Add(new KnownGame(
                Id: $"steam.{sg.AppId}",
                DisplayName: sg.Name,
                ExecutableNames: exes,
                SteamAppId: sg.AppId,
                InstallPath: sg.PrimaryExecutablePath,
                Source: KnownGameSource.Steam));
            seenSteamIds.Add(sg.AppId);
        }

        // Static catalog — skip ones already in Steam
        foreach (var g in StaticGameCatalog.All)
        {
            if (g.SteamAppId is not null && seenSteamIds.Contains(g.SteamAppId)) continue;
            result.Add(g);
        }

        // User-added — never deduped
        foreach (var g in await _added.LoadAsync())
            result.Add(g);

        return result;
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~GameRegistryTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add GameRegistry merging Steam, static catalog, and user-added games"
```

---

### Task 28: GameProfileAssignment + GameProfileStore

**Files:**
- Create: `src/PrimeOSTuner.Core/Games/GameProfileAssignment.cs`
- Create: `src/PrimeOSTuner.Core/Games/GameProfileStore.cs`
- Create: `src/PrimeOSTuner.Tests/Games/GameProfileStoreTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Games/GameProfileStoreTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class GameProfileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gp-{Guid.NewGuid()}.json");
    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task GetProfileFor_returns_null_when_not_assigned()
    {
        var store = new GameProfileStore(_path);
        (await store.GetProfileForAsync("steam.440")).Should().BeNull();
    }

    [Fact]
    public async Task SetProfileFor_then_GetProfileFor_round_trips()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "performance");

        var p = await store.GetProfileForAsync("steam.440");
        p.Should().Be("performance");
    }

    [Fact]
    public async Task SetProfileFor_overwrites_existing_assignment()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "basic");
        await store.SetProfileForAsync("steam.440", "performance");

        (await store.GetProfileForAsync("steam.440")).Should().Be("performance");
    }

    [Fact]
    public async Task ClearProfileFor_removes_assignment()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "basic");
        await store.ClearProfileForAsync("steam.440");

        (await store.GetProfileForAsync("steam.440")).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~GameProfileStoreTests
```

- [ ] **Step 3: Implement record and store**

`src/PrimeOSTuner.Core/Games/GameProfileAssignment.cs`:

```csharp
namespace PrimeOSTuner.Core.Games;

public sealed record GameProfileAssignment(string GameId, string ModeName);
```

`src/PrimeOSTuner.Core/Games/GameProfileStore.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Games;

public sealed class GameProfileStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GameProfileStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner",
            "game-profiles.json");

    public async Task<IReadOnlyList<GameProfileAssignment>> LoadAsync()
    {
        if (!File.Exists(_path)) return Array.Empty<GameProfileAssignment>();
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<List<GameProfileAssignment>>(json) ?? new();
        }
        catch
        {
            return Array.Empty<GameProfileAssignment>();
        }
    }

    public async Task<string?> GetProfileForAsync(string gameId)
    {
        var entries = await LoadAsync();
        return entries.FirstOrDefault(e => e.GameId == gameId)?.ModeName;
    }

    public async Task SetProfileForAsync(string gameId, string modeName)
    {
        var list = (await LoadAsync()).Where(e => e.GameId != gameId).ToList();
        list.Add(new GameProfileAssignment(gameId, modeName));
        await SaveAsync(list);
    }

    public async Task ClearProfileForAsync(string gameId)
    {
        var list = (await LoadAsync()).Where(e => e.GameId != gameId).ToList();
        await SaveAsync(list);
    }

    private async Task SaveAsync(List<GameProfileAssignment> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(list, JsonOpts));
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~GameProfileStoreTests
```

Expected: `Passed! - 4 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add GameProfileStore mapping game IDs to mode names"
```

---


## Phase F — Game Process Watcher

### Task 29: IGameProcessWatcher + GameProcessWatcher

**Files:**
- Create: `src/PrimeOSTuner.Core/Lifecycle/IGameProcessWatcher.cs`
- Create: `src/PrimeOSTuner.Core/Lifecycle/GameProcessWatcher.cs`
- Create: `src/PrimeOSTuner.Tests/Lifecycle/GameProcessWatcherTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Lifecycle/GameProcessWatcherTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using Xunit;

namespace PrimeOSTuner.Tests.Lifecycle;

public class GameProcessWatcherTests
{
    private static KnownGame Tf2 = new(
        "steam.440", "Team Fortress 2",
        new[] { "hl2.exe" }, "440", null, KnownGameSource.Steam);

    [Fact]
    public async Task Tick_raises_GameStarted_when_known_executable_appears()
    {
        var snapshot = new List<string> { "explorer.exe", "chrome.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        KnownGame? started = null;
        watcher.GameStarted += (_, g) => started = g;

        await watcher.TickAsync(); // none running
        snapshot.Add("hl2.exe");
        await watcher.TickAsync();

        started.Should().NotBeNull();
        started!.Id.Should().Be("steam.440");
    }

    [Fact]
    public async Task Tick_raises_GameStopped_when_executable_disappears()
    {
        var snapshot = new List<string> { "hl2.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        KnownGame? stopped = null;
        watcher.GameStopped += (_, e) => stopped = e.Game;

        await watcher.TickAsync(); // detected as started
        snapshot.Clear();
        await watcher.TickAsync(); // detected as stopped

        stopped.Should().NotBeNull();
        stopped!.Id.Should().Be("steam.440");
    }

    [Fact]
    public async Task Tick_does_not_raise_GameStarted_twice_for_same_game()
    {
        var snapshot = new List<string> { "hl2.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        var startCount = 0;
        watcher.GameStarted += (_, _) => startCount++;

        await watcher.TickAsync();
        await watcher.TickAsync();
        await watcher.TickAsync();

        startCount.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~GameProcessWatcherTests
```

- [ ] **Step 3: Implement interface**

`src/PrimeOSTuner.Core/Lifecycle/IGameProcessWatcher.cs`:

```csharp
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed record GameStoppedArgs(KnownGame Game, string Reason);

public interface IGameProcessWatcher
{
    event EventHandler<KnownGame>? GameStarted;
    event EventHandler<GameStoppedArgs>? GameStopped;

    void Start();
    void Stop();
    bool IsRunning { get; }
    Task TickAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement watcher**

`src/PrimeOSTuner.Core/Lifecycle/GameProcessWatcher.cs`:

```csharp
using System.Diagnostics;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed class GameProcessWatcher : IGameProcessWatcher, IDisposable
{
    private readonly Func<Task<IReadOnlyList<KnownGame>>> _knownGamesProvider;
    private readonly Func<string[]> _processSnapshotProvider;
    private readonly int _pollIntervalMs;
    private readonly System.Timers.Timer _timer;
    private readonly Dictionary<string, KnownGame> _running = new();

    public event EventHandler<KnownGame>? GameStarted;
    public event EventHandler<GameStoppedArgs>? GameStopped;

    public bool IsRunning { get; private set; }

    public GameProcessWatcher(
        Func<Task<IReadOnlyList<KnownGame>>> knownGamesProvider,
        Func<string[]>? processSnapshotProvider = null,
        int pollIntervalMs = 2000)
    {
        _knownGamesProvider = knownGamesProvider;
        _processSnapshotProvider = processSnapshotProvider ?? DefaultSnapshot;
        _pollIntervalMs = pollIntervalMs;
        _timer = new System.Timers.Timer(_pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += async (_, _) => { try { await TickAsync(); } catch { } };
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _timer.Start();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
    }

    public async Task TickAsync(CancellationToken ct = default)
    {
        var known = await _knownGamesProvider();
        var processNames = new HashSet<string>(_processSnapshotProvider(), StringComparer.OrdinalIgnoreCase);

        foreach (var game in known)
        {
            ct.ThrowIfCancellationRequested();
            var hasExe = game.ExecutableNames.Any(e => processNames.Contains(e));
            var alreadyTracked = _running.ContainsKey(game.Id);

            if (hasExe && !alreadyTracked)
            {
                _running[game.Id] = game;
                GameStarted?.Invoke(this, game);
            }
            else if (!hasExe && alreadyTracked)
            {
                _running.Remove(game.Id);
                GameStopped?.Invoke(this, new GameStoppedArgs(game, "process exit"));
            }
        }
    }

    private static string[] DefaultSnapshot()
    {
        try
        {
            return Process.GetProcesses()
                .Select(p => { try { return p.ProcessName + ".exe"; } catch { return ""; } finally { p.Dispose(); } })
                .Where(s => s.Length > 4)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
```

- [ ] **Step 5: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~GameProcessWatcherTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add GameProcessWatcher polling for known executables"
```

---

## Phase G — Profile Lifecycle Service

### Task 30: ProfileLifecycleService — auto-apply / auto-revert + crash recovery

**Files:**
- Create: `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`
- Create: `src/PrimeOSTuner.Tests/Lifecycle/ProfileLifecycleServiceTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/Lifecycle/ProfileLifecycleServiceTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Lifecycle;

public class ProfileLifecycleServiceTests : IDisposable
{
    private readonly string _activePath = Path.Combine(Path.GetTempPath(), $"active-{Guid.NewGuid()}.json");
    private readonly string _gameProfilesPath = Path.Combine(Path.GetTempPath(), $"gp-{Guid.NewGuid()}.json");
    private readonly string _historyPath = Path.Combine(Path.GetTempPath(), $"h-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        foreach (var p in new[] { _activePath, _gameProfilesPath, _historyPath })
            if (File.Exists(p)) File.Delete(p);
    }

    private static Mock<ITweak> StubTweak(string id)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(TweakResult.Success("undo-" + id));
        m.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .ReturnsAsync(TweakResult.Success());
        return m;
    }

    private static KnownGame Game = new(
        "steam.440", "Team Fortress 2", new[] { "hl2.exe" }, "440", null, KnownGameSource.Steam);

    [Fact]
    public async Task On_GameStarted_applies_assigned_profile_and_writes_active_tweaks_file()
    {
        var tweak = StubTweak("game.game-mode").Object;
        var applier = new ProfileApplier(new[] { tweak }, new TweakHistory(_historyPath));
        var profileStore = new GameProfileStore(_gameProfilesPath);
        await profileStore.SetProfileForAsync(Game.Id, "basic");
        var activeStore = new ActiveTweaksStore(_activePath);

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            profileStore,
            activeStore,
            new Dictionary<string, ModeProfile>
            {
                ["basic"] = new ModeProfile("basic", "Basic", "", new[] { "game.game-mode" })
            },
            applier);

        service.Start();
        watcher.Raise(w => w.GameStarted += null, this, Game);
        await Task.Delay(100);

        var record = await activeStore.LoadAsync();
        record.Should().NotBeNull();
        record!.GameId.Should().Be(Game.Id);
        record.Outcomes.Should().HaveCount(1);
    }

    [Fact]
    public async Task On_GameStopped_reverts_outcomes_and_clears_active_tweaks_file()
    {
        var tweak = StubTweak("game.game-mode");
        var applier = new ProfileApplier(new[] { tweak.Object }, new TweakHistory(_historyPath));
        var profileStore = new GameProfileStore(_gameProfilesPath);
        await profileStore.SetProfileForAsync(Game.Id, "basic");
        var activeStore = new ActiveTweaksStore(_activePath);

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            profileStore,
            activeStore,
            new Dictionary<string, ModeProfile>
            {
                ["basic"] = new ModeProfile("basic", "Basic", "", new[] { "game.game-mode" })
            },
            applier);

        service.Start();
        watcher.Raise(w => w.GameStarted += null, this, Game);
        await Task.Delay(50);
        watcher.Raise(w => w.GameStopped += null, this, new GameStoppedArgs(Game, "exit"));
        await Task.Delay(100);

        tweak.Verify(t => t.RevertAsync("undo-game.game-mode", default), Times.Once);
        (await activeStore.LoadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RecoverFromCrashAsync_reverts_any_existing_active_tweaks_record()
    {
        var tweak = StubTweak("game.game-mode");
        var applier = new ProfileApplier(new[] { tweak.Object }, new TweakHistory(_historyPath));
        var activeStore = new ActiveTweaksStore(_activePath);
        await activeStore.SaveAsync(new ActiveTweaksRecord(
            Game.Id, "basic", DateTime.UtcNow,
            new[] { new ProfileTweakOutcome("game.game-mode", true, "undo-game.game-mode", null) }));

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            new GameProfileStore(_gameProfilesPath),
            activeStore,
            new Dictionary<string, ModeProfile>(),
            applier);

        await service.RecoverFromCrashAsync();

        tweak.Verify(t => t.RevertAsync("undo-game.game-mode", default), Times.Once);
        (await activeStore.LoadAsync()).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ProfileLifecycleServiceTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`:

```csharp
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Profiles;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed class ProfileLifecycleService
{
    private readonly IGameProcessWatcher _watcher;
    private readonly GameProfileStore _profiles;
    private readonly ActiveTweaksStore _active;
    private readonly IReadOnlyDictionary<string, ModeProfile> _profileLookup;
    private readonly ProfileApplier _applier;

    public ProfileLifecycleService(
        IGameProcessWatcher watcher,
        GameProfileStore profiles,
        ActiveTweaksStore active,
        IReadOnlyDictionary<string, ModeProfile> profileLookup,
        ProfileApplier applier)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
    }

    public void Start()
    {
        _watcher.GameStarted += OnGameStarted;
        _watcher.GameStopped += OnGameStopped;
        _watcher.Start();
    }

    public void Stop()
    {
        _watcher.GameStarted -= OnGameStarted;
        _watcher.GameStopped -= OnGameStopped;
        _watcher.Stop();
    }

    public async Task RecoverFromCrashAsync(CancellationToken ct = default)
    {
        var record = await _active.LoadAsync();
        if (record is null) return;

        await _applier.RevertAsync(record.Outcomes, ct);
        await _active.ClearAsync();
    }

    private async void OnGameStarted(object? sender, KnownGame game)
    {
        try
        {
            var modeName = await _profiles.GetProfileForAsync(game.Id);
            if (modeName is null) return;
            if (!_profileLookup.TryGetValue(modeName, out var profile)) return;

            var result = await _applier.ApplyAsync(profile);
            await _active.SaveAsync(new ActiveTweaksRecord(
                game.Id, profile.Id, DateTime.UtcNow, result.Outcomes));
        }
        catch
        {
            // log via Serilog if registered; never throw out of an event handler
        }
    }

    private async void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        try
        {
            var record = await _active.LoadAsync();
            if (record is null || record.GameId != e.Game.Id) return;

            await _applier.RevertAsync(record.Outcomes);
            await _active.ClearAsync();
        }
        catch
        {
            // never throw
        }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ProfileLifecycleServiceTests
```

Expected: `Passed! - 3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add ProfileLifecycleService binding watcher events to apply/revert"
```

---


## Phase H — Game Library UI

### Task 31: DI registration of all v0.2 services

**Files:**
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 1: Update `App.xaml.cs` `ConfigureServices`** — append the new registrations after the v0.1 ones. Add these `using`s at the top:

```csharp
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Win.Network;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;
```

Inside `ConfigureServices(s => { ... })`, append after existing registrations:

```csharp
// Win-layer additions
s.AddSingleton<INetworkInterfaceClient, NetworkInterfaceClient>();
s.AddSingleton<ITimerResolutionClient, TimerResolutionClient>();
s.AddSingleton<ISteamLibraryScanner, SteamLibraryScanner>();
s.AddSingleton(_ => SteamGridDbSettings.Load());
s.AddHttpClient<ISteamGridDbClient, SteamGridDbClient>(c =>
{
    c.BaseAddress = new Uri("https://www.steamgriddb.com");
    c.Timeout = TimeSpan.FromSeconds(20);
});
s.AddSingleton<ArtCache>(sp =>
    new ArtCache(ArtCache.DefaultDir(),
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("art-download")));
s.AddHttpClient("art-download");

// Core additions — new tweaks
s.AddSingleton<MouseAccelTweak>();
s.AddSingleton<TimerResolutionTweak>();
s.AddSingleton<GameModeTweak>();
s.AddSingleton<HwGpuSchedulingTweak>();
s.AddSingleton<NagleAlgorithmTweak>();
s.AddSingleton<NetworkThrottlingIndexTweak>();
s.AddSingleton<SystemResponsivenessTweak>();
s.AddSingleton<CpuCoreParkingTweak>();
// PerAppGpuPreferenceTweak depends on a list of exe paths gathered at runtime; resolve it lazily:
s.AddSingleton<Func<IEnumerable<string>, PerAppGpuPreferenceTweak>>(sp =>
    paths => new PerAppGpuPreferenceTweak(sp.GetRequiredService<IRegistryClient>(), paths));

// Replace the v0.1 IEnumerable<ITweak> registration with the full v0.2 set:
s.AddSingleton<IEnumerable<ITweak>>(sp =>
{
    var perAppFactory = sp.GetRequiredService<Func<IEnumerable<string>, PerAppGpuPreferenceTweak>>();
    var registry = sp.GetRequiredService<GameRegistry>();
    var gamePaths = registry.GetAllAsync().GetAwaiter().GetResult()
        .Where(g => g.InstallPath is not null)
        .Select(g => g.InstallPath!)
        .ToList();
    return new ITweak[]
    {
        sp.GetRequiredService<JunkFileTweak>(),
        sp.GetRequiredService<PowerPlanTweak>(),
        sp.GetRequiredService<RamCleanerTweak>(),
        sp.GetRequiredService<VisualEffectsTweak>(),
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
});

// Profiles
s.AddSingleton(_ => new CustomProfileStore(CustomProfileStore.DefaultPath()));
s.AddSingleton(_ => new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()));
s.AddSingleton<ProfileApplier>();

// Games
s.AddSingleton(_ => new AddedGamesStore(AddedGamesStore.DefaultPath()));
s.AddSingleton<GameRegistry>();
s.AddSingleton(_ => new GameProfileStore(GameProfileStore.DefaultPath()));

// Lifecycle
s.AddSingleton<IGameProcessWatcher>(sp =>
{
    var registry = sp.GetRequiredService<GameRegistry>();
    return new GameProcessWatcher(
        knownGamesProvider: () => registry.GetAllAsync(),
        processSnapshotProvider: null,
        pollIntervalMs: 2000);
});
s.AddSingleton<ProfileLifecycleService>(sp =>
{
    var custom = sp.GetRequiredService<CustomProfileStore>();
    var customProfile = custom.LoadAsync().GetAwaiter().GetResult();
    var dict = new Dictionary<string, ModeProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["basic"] = BuiltInProfiles.Basic,
        ["performance"] = BuiltInProfiles.Performance,
        ["custom"] = customProfile,
    };
    return new ProfileLifecycleService(
        sp.GetRequiredService<IGameProcessWatcher>(),
        sp.GetRequiredService<GameProfileStore>(),
        sp.GetRequiredService<ActiveTweaksStore>(),
        dict,
        sp.GetRequiredService<ProfileApplier>());
});

// ViewModels (registered later as they are written)
```

- [ ] **Step 2: After `Host.Start()`, before `window.Show()`, run crash-recovery and start the watcher**

```csharp
var lifecycle = Host.Services.GetRequiredService<ProfileLifecycleService>();
await lifecycle.RecoverFromCrashAsync();
lifecycle.Start();
```

Note: change `OnStartup` to `protected override async void OnStartup(StartupEventArgs e)` so it can `await`.

- [ ] **Step 3: Build to confirm compiles**

```powershell
dotnet build src/PrimeOSTuner.UI
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "Wire v0.2 services into the DI container with crash recovery"
```

---

### Task 32: GameTileViewModel

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/GameTileViewModel.cs`

- [ ] **Step 1: Implement directly**

`src/PrimeOSTuner.UI/ViewModels/GameTileViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameTileViewModel : ObservableObject
{
    [ObservableProperty] private string? _coverImagePath;
    [ObservableProperty] private bool _isLoadingCover;
    [ObservableProperty] private string _assignedMode = "(none)";
    [ObservableProperty] private bool _isRunning;

    public KnownGame Game { get; }

    public string DisplayName => Game.DisplayName;
    public string Id => Game.Id;

    public GameTileViewModel(KnownGame game) { Game = game; IsLoadingCover = true; }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 3: Commit**

```powershell
git add .
git commit -m "Add GameTileViewModel"
```

---

### Task 33: GameLibraryViewModel — load games + cover art + assignments

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/GameLibraryViewModel.cs`
- Create: `src/PrimeOSTuner.Tests/ViewModels/GameLibraryViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/ViewModels/GameLibraryViewModelTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win.SteamGridDb;
using PrimeOSTuner.Win.Steam;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class GameLibraryViewModelTests : IDisposable
{
    private readonly string _addedPath = Path.Combine(Path.GetTempPath(), $"vm-added-{Guid.NewGuid()}.json");
    private readonly string _gpPath = Path.Combine(Path.GetTempPath(), $"vm-gp-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        foreach (var p in new[] { _addedPath, _gpPath })
            if (File.Exists(p)) File.Delete(p);
    }

    [Fact]
    public async Task LoadAsync_populates_tiles_for_each_game_in_registry()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var registry = new GameRegistry(scanner.Object, new AddedGamesStore(_addedPath));
        var profileStore = new GameProfileStore(_gpPath);
        var sgdb = new Mock<ISteamGridDbClient>();
        sgdb.SetupGet(c => c.HasApiKey).Returns(false);

        var vm = new GameLibraryViewModel(registry, profileStore, sgdb.Object, artCache: null);
        await vm.LoadAsync();

        vm.Tiles.Should().NotBeEmpty();
        vm.Tiles.Should().Contain(t => t.Id == "static.valorant");
    }

    [Fact]
    public async Task LoadAsync_attaches_assigned_mode_from_profile_store()
    {
        var scanner = new Mock<ISteamLibraryScanner>();
        scanner.Setup(s => s.ScanInstalledGames()).Returns(Array.Empty<SteamGame>());
        var registry = new GameRegistry(scanner.Object, new AddedGamesStore(_addedPath));
        var profileStore = new GameProfileStore(_gpPath);
        await profileStore.SetProfileForAsync("static.valorant", "performance");
        var sgdb = new Mock<ISteamGridDbClient>();
        sgdb.SetupGet(c => c.HasApiKey).Returns(false);

        var vm = new GameLibraryViewModel(registry, profileStore, sgdb.Object, artCache: null);
        await vm.LoadAsync();

        var tile = vm.Tiles.First(t => t.Id == "static.valorant");
        tile.AssignedMode.Should().Be("performance");
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~GameLibraryViewModelTests
```

- [ ] **Step 3: Implement**

`src/PrimeOSTuner.UI/ViewModels/GameLibraryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.SteamGridDb;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameLibraryViewModel : ObservableObject
{
    private readonly GameRegistry _registry;
    private readonly GameProfileStore _profiles;
    private readonly ISteamGridDbClient _sgdb;
    private readonly ArtCache? _art;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showApiKeyPrompt;

    public ObservableCollection<GameTileViewModel> Tiles { get; } = new();

    public GameLibraryViewModel(
        GameRegistry registry,
        GameProfileStore profiles,
        ISteamGridDbClient sgdb,
        ArtCache? artCache)
    {
        _registry = registry;
        _profiles = profiles;
        _sgdb = sgdb;
        _art = artCache;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Tiles.Clear();

        var games = await _registry.GetAllAsync();
        foreach (var g in games)
        {
            var tile = new GameTileViewModel(g);
            var assigned = await _profiles.GetProfileForAsync(g.Id);
            tile.AssignedMode = assigned ?? "(none)";
            Tiles.Add(tile);
        }

        ShowApiKeyPrompt = !_sgdb.HasApiKey;
        IsLoading = false;

        // Fire and forget cover-art load
        _ = LoadCoversAsync();
    }

    private async Task LoadCoversAsync()
    {
        if (_art is null || !_sgdb.HasApiKey) return;
        foreach (var tile in Tiles.ToList())
        {
            try
            {
                CoverArt art;
                if (tile.Game.SteamAppId is not null)
                    art = await _sgdb.GetCoverByAppIdAsync(tile.Game.SteamAppId);
                else
                {
                    var hits = await _sgdb.SearchAsync(tile.Game.DisplayName);
                    var first = hits.FirstOrDefault();
                    art = first is null
                        ? new CoverArt(null, tile.Game.DisplayName, null, null)
                        : await _sgdb.GetCoverByGameIdAsync(first.Id, first.Name);
                }

                if (art.GameId is not null && art.Url is not null)
                {
                    var path = await _art.GetOrDownloadAsync(art.GameId.Value, art.Url);
                    var dispatcher = Application.Current?.Dispatcher;
                    Action update = () => { tile.CoverImagePath = path; tile.IsLoadingCover = false; };
                    if (dispatcher is null || dispatcher.CheckAccess()) update();
                    else dispatcher.Invoke(update);
                }
                else
                {
                    tile.IsLoadingCover = false;
                }
            }
            catch
            {
                tile.IsLoadingCover = false;
            }
        }
    }
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~GameLibraryViewModelTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add GameLibraryViewModel loading registry, profiles, and cover art"
```

---

### Task 34: GameCard user control

**Files:**
- Create: `src/PrimeOSTuner.UI/Controls/GameCard.xaml`
- Create: `src/PrimeOSTuner.UI/Controls/GameCard.xaml.cs`

- [ ] **Step 1: Create XAML**

`src/PrimeOSTuner.UI/Controls/GameCard.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.GameCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="180" Height="300" Cursor="Hand">
    <Border Background="{StaticResource Bg2Brush}" CornerRadius="12" BorderBrush="{StaticResource LineBrush}" BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <Grid Grid.Row="0">
                <!-- Skeleton shown while CoverImagePath is null -->
                <Border x:Name="Skeleton"
                        Background="{StaticResource Bg3Brush}" CornerRadius="12,12,0,0"
                        Visibility="{Binding IsLoadingCover, Converter={StaticResource BoolToVisibility}}"/>
                <Image x:Name="Cover"
                       Source="{Binding CoverImagePath}"
                       Stretch="UniformToFill"/>
                <Border Background="#80000000" VerticalAlignment="Bottom" Height="80" CornerRadius="0,0,0,0"/>
                <StackPanel VerticalAlignment="Bottom" Margin="10,0,10,10">
                    <TextBlock Text="{Binding DisplayName}" Foreground="{StaticResource Text0Brush}" FontWeight="Bold" FontSize="14" TextWrapping="Wrap"/>
                    <TextBlock Text="{Binding AssignedMode}" Foreground="{StaticResource AccentBrush}" FontSize="11"/>
                </StackPanel>
            </Grid>

            <ComboBox Grid.Row="1" x:Name="ProfileCombo"
                      SelectionChanged="ProfileCombo_SelectionChanged"
                      Margin="6" Padding="8,4">
                <ComboBoxItem Content="(none)"/>
                <ComboBoxItem Content="basic"/>
                <ComboBoxItem Content="performance"/>
                <ComboBoxItem Content="custom"/>
            </ComboBox>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: Create code-behind**

`src/PrimeOSTuner.UI/Controls/GameCard.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Controls;

public partial class GameCard : UserControl
{
    public event EventHandler<(string GameId, string ModeName)>? ProfileChanged;

    public GameCard()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncCombo();
    }

    private void SyncCombo()
    {
        if (DataContext is GameTileViewModel vm)
        {
            for (int i = 0; i < ProfileCombo.Items.Count; i++)
            {
                if (((ComboBoxItem)ProfileCombo.Items[i]).Content?.ToString() == vm.AssignedMode)
                {
                    ProfileCombo.SelectedIndex = i;
                    return;
                }
            }
            ProfileCombo.SelectedIndex = 0; // (none)
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameTileViewModel vm) return;
        if (ProfileCombo.SelectedItem is not ComboBoxItem item) return;
        var mode = item.Content?.ToString() ?? "(none)";
        if (vm.AssignedMode == mode) return;
        vm.AssignedMode = mode;
        ProfileChanged?.Invoke(this, (vm.Id, mode));
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "Add GameCard user control with cover art and profile picker"
```

---

### Task 35: GameLibraryView XAML

**Files:**
- Create: `src/PrimeOSTuner.UI/Views/GameLibraryView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/GameLibraryView.xaml.cs`

- [ ] **Step 1: Create XAML**

`src/PrimeOSTuner.UI/Views/GameLibraryView.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.GameLibraryView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:c="clr-namespace:PrimeOSTuner.UI.Controls">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <Grid Grid.Row="0" Margin="0,0,0,18">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="Game Library" Style="{StaticResource HeaderText}"/>
            <Button Grid.Column="1" Content="+ Add Game" Click="AddGameClick"
                    Padding="14,6" Background="{StaticResource Bg2Brush}" Foreground="{StaticResource Text0Brush}"
                    BorderBrush="{StaticResource LineBrush}" BorderThickness="1"/>
        </Grid>

        <Border Grid.Row="1" Style="{StaticResource CardBorder}" Margin="0,0,0,12"
                Visibility="{Binding ShowApiKeyPrompt, Converter={StaticResource BoolToVisibility}}">
            <StackPanel>
                <TextBlock Text="Cover art disabled" Foreground="{StaticResource WarnBrush}" FontWeight="Bold"/>
                <TextBlock Text="Add a SteamGridDB API key to %LOCALAPPDATA%\PrimeOSTuner\settings.json under SteamGridDbApiKey to enable cover art."
                           Foreground="{StaticResource Text2Brush}" FontSize="12" TextWrapping="Wrap"/>
            </StackPanel>
        </Border>

        <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
            <ItemsControl ItemsSource="{Binding Tiles}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <c:GameCard Margin="6" ProfileChanged="GameCard_ProfileChanged"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Add `WarnBrush` to `Theme/Colors.xaml`** — append before the closing `</ResourceDictionary>`:

```xml
<SolidColorBrush x:Key="WarnBrush" Color="{StaticResource WarnColor}"/>
```

- [ ] **Step 3: Create code-behind**

`src/PrimeOSTuner.UI/Views/GameLibraryView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class GameLibraryView : UserControl
{
    private readonly GameLibraryViewModel _vm;
    private readonly GameProfileStore _profiles;
    private readonly AddedGamesStore _added;

    public GameLibraryView(GameLibraryViewModel vm, GameProfileStore profiles, AddedGamesStore added)
    {
        InitializeComponent();
        _vm = vm;
        _profiles = profiles;
        _added = added;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private async void GameCard_ProfileChanged(object? sender, (string GameId, string ModeName) e)
    {
        if (e.ModeName == "(none)")
            await _profiles.ClearProfileForAsync(e.GameId);
        else
            await _profiles.SetProfileForAsync(e.GameId, e.ModeName);
    }

    private async void AddGameClick(object sender, RoutedEventArgs e)
    {
        var sp = ((App)Application.Current).Host.Services;
        var dialog = sp.GetRequiredService<AddGameDialog>();
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            await _added.AddAsync(dialog.Result);
            await _vm.LoadAsync();
        }
    }
}
```

- [ ] **Step 4: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddSingleton<GameLibraryViewModel>();
s.AddTransient<Views.GameLibraryView>();
```

- [ ] **Step 5: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

(The `AddGameDialog` will be created in the next task — this build will fail. Skip the build step until Task 36 is complete, then build again.)

- [ ] **Step 6: Commit (after Task 36 builds clean)**

```powershell
git add .
git commit -m "Add GameLibraryView with WrapPanel of GameCards and Add Game button"
```

---

### Task 36: AddGameDialog

**Files:**
- Create: `src/PrimeOSTuner.UI/Dialogs/AddGameDialog.xaml`
- Create: `src/PrimeOSTuner.UI/Dialogs/AddGameDialog.xaml.cs`

- [ ] **Step 1: Create XAML**

`src/PrimeOSTuner.UI/Dialogs/AddGameDialog.xaml`:

```xml
<Window x:Class="PrimeOSTuner.UI.Dialogs.AddGameDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add a game" Height="320" Width="500" WindowStartupLocation="CenterOwner"
        Background="{StaticResource Bg0Brush}">
    <StackPanel Margin="22">
        <TextBlock Text="Display name" Style="{StaticResource SectionLabel}"/>
        <TextBox x:Name="NameBox" Margin="0,0,0,12" Padding="8,6"
                 Background="{StaticResource Bg2Brush}" Foreground="{StaticResource Text0Brush}"
                 BorderBrush="{StaticResource LineBrush}"/>

        <TextBlock Text="Executable path" Style="{StaticResource SectionLabel}"/>
        <Grid Margin="0,0,0,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBox Grid.Column="0" x:Name="ExeBox" Padding="8,6"
                     Background="{StaticResource Bg2Brush}" Foreground="{StaticResource Text0Brush}"
                     BorderBrush="{StaticResource LineBrush}"/>
            <Button Grid.Column="1" Content="Browse..." Click="BrowseClick" Margin="6,0,0,0" Padding="10,4"/>
        </Grid>

        <TextBlock Text="Steam app ID (optional)" Style="{StaticResource SectionLabel}"/>
        <TextBox x:Name="AppIdBox" Margin="0,0,0,18" Padding="8,6"
                 Background="{StaticResource Bg2Brush}" Foreground="{StaticResource Text0Brush}"
                 BorderBrush="{StaticResource LineBrush}"/>

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" Click="CancelClick" Margin="0,0,8,0" Padding="14,6"/>
            <Button Content="Add" Click="OkClick" Padding="14,6"
                    Background="{StaticResource AccentBrush}" Foreground="#001b17" FontWeight="Bold"/>
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Create code-behind**

`src/PrimeOSTuner.UI/Dialogs/AddGameDialog.xaml.cs`:

```csharp
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.UI.Dialogs;

public partial class AddGameDialog : Window
{
    public KnownGame? Result { get; private set; }

    public AddGameDialog()
    {
        InitializeComponent();
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe",
            Title = "Select the game's executable"
        };
        if (dlg.ShowDialog() == true)
        {
            ExeBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        var exe = ExeBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exe))
        {
            MessageBox.Show("Please enter both a name and an executable path.", "Missing info");
            return;
        }
        var exeName = Path.GetFileName(exe);
        var id = "user." + name.ToLowerInvariant().Replace(" ", "-");
        var appId = string.IsNullOrWhiteSpace(AppIdBox.Text) ? null : AppIdBox.Text.Trim();
        Result = new KnownGame(id, name, new[] { exeName }, appId, exe, KnownGameSource.UserAdded);
        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
```

- [ ] **Step 3: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddTransient<Dialogs.AddGameDialog>();
```

- [ ] **Step 4: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit (covers both Task 35 and 36)**

```powershell
git add .
git commit -m "Add AddGameDialog for manually adding non-Steam games"
```

---

## Phase I — Custom Mode UI

### Task 37: CustomModeViewModel + CustomModeView

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/CustomModeViewModel.cs`
- Create: `src/PrimeOSTuner.UI/Views/CustomModeView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/CustomModeView.xaml.cs`
- Create: `src/PrimeOSTuner.Tests/ViewModels/CustomModeViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/ViewModels/CustomModeViewModelTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class CustomModeViewModelTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid()}.json");
    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    private static Mock<ITweak> Stub(string id, string name)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(name);
        m.SetupGet(t => t.Description).Returns($"Description of {name}");
        return m;
    }

    [Fact]
    public async Task LoadAsync_marks_tweaks_already_in_custom_profile_as_selected()
    {
        var store = new CustomProfileStore(_path);
        await store.SaveAsync(new[] { "a" });
        var tweaks = new[] { Stub("a", "A").Object, Stub("b", "B").Object };

        var vm = new CustomModeViewModel(tweaks, store);
        await vm.LoadAsync();

        vm.Items.First(i => i.Id == "a").IsSelected.Should().BeTrue();
        vm.Items.First(i => i.Id == "b").IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_writes_only_selected_ids_to_store()
    {
        var store = new CustomProfileStore(_path);
        var tweaks = new[] { Stub("a", "A").Object, Stub("b", "B").Object };
        var vm = new CustomModeViewModel(tweaks, store);
        await vm.LoadAsync();

        vm.Items.First(i => i.Id == "b").IsSelected = true;
        await vm.SaveAsync();

        var loaded = await store.LoadAsync();
        loaded.TweakIds.Should().BeEquivalentTo(new[] { "b" });
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~CustomModeViewModelTests
```

- [ ] **Step 3: Implement view-model**

`src/PrimeOSTuner.UI/ViewModels/CustomModeViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.ViewModels;

public partial class CustomModeItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public CustomModeItem(ITweak t)
    {
        Id = t.Id; DisplayName = t.DisplayName; Description = t.Description;
    }
}

public partial class CustomModeViewModel : ObservableObject
{
    private readonly IEnumerable<ITweak> _tweaks;
    private readonly CustomProfileStore _store;

    public ObservableCollection<CustomModeItem> Items { get; } = new();

    public CustomModeViewModel(IEnumerable<ITweak> tweaks, CustomProfileStore store)
    {
        _tweaks = tweaks;
        _store = store;
    }

    public async Task LoadAsync()
    {
        Items.Clear();
        var current = await _store.LoadAsync();
        var selected = new HashSet<string>(current.TweakIds);
        foreach (var t in _tweaks)
        {
            var item = new CustomModeItem(t) { IsSelected = selected.Contains(t.Id) };
            Items.Add(item);
        }
    }

    public Task SaveAsync()
    {
        var ids = Items.Where(i => i.IsSelected).Select(i => i.Id);
        return _store.SaveAsync(ids);
    }
}
```

- [ ] **Step 4: Implement view XAML**

`src/PrimeOSTuner.UI/Views/CustomModeView.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.CustomModeView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="Custom Mode" Style="{StaticResource HeaderText}" Margin="0,0,0,18"/>
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Style="{StaticResource CardBorder}" Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}" VerticalAlignment="Top" Margin="0,2,12,0"/>
                                <StackPanel Grid.Column="1">
                                    <TextBlock Text="{Binding DisplayName}" Foreground="{StaticResource Text0Brush}" FontWeight="Bold"/>
                                    <TextBlock Text="{Binding Description}" Foreground="{StaticResource Text2Brush}" FontSize="11" TextWrapping="Wrap" Margin="0,4,0,0"/>
                                </StackPanel>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Save Custom Mode" Click="SaveClick" Padding="18,8"
                    Background="{StaticResource AccentBrush}" Foreground="#001b17" FontWeight="Bold" BorderThickness="0"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 5: Implement view code-behind**

`src/PrimeOSTuner.UI/Views/CustomModeView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class CustomModeView : UserControl
{
    private readonly CustomModeViewModel _vm;

    public CustomModeView(CustomModeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        await _vm.SaveAsync();
        MessageBox.Show("Custom Mode saved. Apply it from Game Boost or assign to a game in Game Library.", "Saved");
    }
}
```

- [ ] **Step 6: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddTransient<CustomModeViewModel>();
s.AddTransient<Views.CustomModeView>();
```

- [ ] **Step 7: Run tests**

```powershell
dotnet test --filter FullyQualifiedName~CustomModeViewModelTests
```

Expected: `Passed! - 2 passed`.

- [ ] **Step 8: Commit**

```powershell
git add .
git commit -m "Add CustomModeView and view-model for editing custom tweak set"
```

---

## Phase J — Game Boost Tab UI

### Task 38: GameBoostViewModel + GameBoostView

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/GameBoostViewModel.cs`
- Create: `src/PrimeOSTuner.UI/Views/GameBoostView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/GameBoostView.xaml.cs`

- [ ] **Step 1: Implement view-model**

`src/PrimeOSTuner.UI/ViewModels/GameBoostViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProfileApplier _applier;
    private readonly CustomProfileStore _customStore;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isWorking;

    public GameBoostViewModel(ProfileApplier applier, CustomProfileStore customStore)
    {
        _applier = applier;
        _customStore = customStore;
    }

    public async Task ApplyBasicAsync()
    {
        await ApplyAsync(BuiltInProfiles.Basic);
    }

    public async Task ApplyPerformanceAsync()
    {
        await ApplyAsync(BuiltInProfiles.Performance);
    }

    public async Task ApplyCustomAsync()
    {
        var profile = await _customStore.LoadAsync();
        if (profile.TweakIds.Count == 0)
        {
            StatusMessage = "Custom Mode is empty — pick tweaks in the Custom Mode tab first.";
            return;
        }
        await ApplyAsync(profile);
    }

    private async Task ApplyAsync(ModeProfile profile)
    {
        IsWorking = true;
        StatusMessage = $"Applying {profile.DisplayName}...";
        try
        {
            var result = await _applier.ApplyAsync(profile);
            StatusMessage = $"{profile.DisplayName}: {result.SuccessCount} applied, {result.FailureCount} failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }
}
```

- [ ] **Step 2: Implement view XAML**

`src/PrimeOSTuner.UI/Views/GameBoostView.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.GameBoostView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Game Boost" Style="{StaticResource HeaderText}" Margin="0,0,0,18"/>

        <UniformGrid Grid.Row="1" Columns="3" Rows="1" Margin="0,0,0,18">
            <Border Style="{StaticResource CardBorder}" Margin="0,0,6,0">
                <StackPanel>
                    <TextBlock Text="BASIC MODE" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}"/>
                    <TextBlock Text="Lightweight gaming preset" Foreground="{StaticResource Text0Brush}" FontWeight="Bold" FontSize="16" Margin="0,4,0,8"/>
                    <TextBlock TextWrapping="Wrap" Foreground="{StaticResource Text2Brush}" FontSize="12">
                        Game Mode + mouse acceleration off + High-Performance power plan + visual effects optimized.
                    </TextBlock>
                    <Button Content="Apply Now" Click="ApplyBasicClick" Margin="0,16,0,0" Padding="14,6"
                            Background="{StaticResource AccentBrush}" Foreground="#001b17" FontWeight="Bold" BorderThickness="0"/>
                </StackPanel>
            </Border>

            <Border Style="{StaticResource CardBorder}" Margin="6,0">
                <StackPanel>
                    <TextBlock Text="PERFORMANCE MODE" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}"/>
                    <TextBlock Text="Maximum tuning preset" Foreground="{StaticResource Text0Brush}" FontWeight="Bold" FontSize="16" Margin="0,4,0,8"/>
                    <TextBlock TextWrapping="Wrap" Foreground="{StaticResource Text2Brush}" FontSize="12">
                        Everything in Basic + 0.5 ms timer + HwGPU scheduling + Nagle off + network throttling off + multimedia priority maxed + per-game GPU prefs.
                    </TextBlock>
                    <Button Content="Apply Now" Click="ApplyPerformanceClick" Margin="0,16,0,0" Padding="14,6"
                            Background="{StaticResource AccentBrush}" Foreground="#001b17" FontWeight="Bold" BorderThickness="0"/>
                </StackPanel>
            </Border>

            <Border Style="{StaticResource CardBorder}" Margin="6,0,0,0">
                <StackPanel>
                    <TextBlock Text="CUSTOM MODE" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}"/>
                    <TextBlock Text="Your hand-picked tweaks" Foreground="{StaticResource Text0Brush}" FontWeight="Bold" FontSize="16" Margin="0,4,0,8"/>
                    <TextBlock TextWrapping="Wrap" Foreground="{StaticResource Text2Brush}" FontSize="12">
                        Select exactly which tweaks to apply on the Custom Mode tab.
                    </TextBlock>
                    <Button Content="Apply Now" Click="ApplyCustomClick" Margin="0,16,0,0" Padding="14,6"
                            Background="{StaticResource AccentBrush}" Foreground="#001b17" FontWeight="Bold" BorderThickness="0"/>
                </StackPanel>
            </Border>
        </UniformGrid>

        <Border Grid.Row="2" Style="{StaticResource CardBorder}">
            <StackPanel>
                <TextBlock Text="STATUS" Style="{StaticResource SectionLabel}"/>
                <TextBlock Text="{Binding StatusMessage}" Foreground="{StaticResource Text1Brush}" FontSize="12"/>
            </StackPanel>
        </Border>

        <TextBlock Grid.Row="3" Margin="0,18,0,0" Foreground="{StaticResource Text3Brush}" FontSize="11" TextWrapping="Wrap">
            Tip: To attach a profile to a specific game so it auto-applies on launch, go to the Game Library tab and pick a profile from the dropdown on each game tile.
        </TextBlock>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Implement code-behind**

`src/PrimeOSTuner.UI/Views/GameBoostView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class GameBoostView : UserControl
{
    private readonly GameBoostViewModel _vm;

    public GameBoostView(GameBoostViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void ApplyBasicClick(object sender, RoutedEventArgs e) => await _vm.ApplyBasicAsync();
    private async void ApplyPerformanceClick(object sender, RoutedEventArgs e) => await _vm.ApplyPerformanceAsync();
    private async void ApplyCustomClick(object sender, RoutedEventArgs e) => await _vm.ApplyCustomAsync();
}
```

- [ ] **Step 4: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddSingleton<GameBoostViewModel>();
s.AddTransient<Views.GameBoostView>();
```

- [ ] **Step 5: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add GameBoostView with Basic/Performance/Custom apply buttons"
```

---

## Phase K — Watcher Status, Nav, and Final Smoke

### Task 39: WatcherStatusViewModel + sidebar bottom toggle

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/WatcherStatusViewModel.cs`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml` (add bottom-of-sidebar status block + new nav buttons)
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`

- [ ] **Step 1: Implement view-model**

`src/PrimeOSTuner.UI/ViewModels/WatcherStatusViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Lifecycle;

namespace PrimeOSTuner.UI.ViewModels;

public partial class WatcherStatusViewModel : ObservableObject
{
    private readonly ProfileLifecycleService _lifecycle;
    private readonly IGameProcessWatcher _watcher;

    [ObservableProperty] private bool _isWatching;
    [ObservableProperty] private string _statusText = "Watching for games";

    public WatcherStatusViewModel(ProfileLifecycleService lifecycle, IGameProcessWatcher watcher)
    {
        _lifecycle = lifecycle;
        _watcher = watcher;
        IsWatching = watcher.IsRunning;
        UpdateText();
    }

    partial void OnIsWatchingChanged(bool value)
    {
        if (value) _lifecycle.Start(); else _lifecycle.Stop();
        UpdateText();
    }

    private void UpdateText()
    {
        StatusText = IsWatching ? "Watching for games" : "Watcher off";
    }
}
```

- [ ] **Step 2: Update `MainWindow.xaml`** — replace the sidebar `<StackPanel>` with this expanded version (also adds Game Boost / Library / Custom Mode buttons):

```xml
<DockPanel Margin="12,20" LastChildFill="True">
    <StackPanel DockPanel.Dock="Bottom" Margin="0,12,0,0">
        <Border Background="{StaticResource Bg2Brush}" CornerRadius="10" Padding="12">
            <StackPanel>
                <TextBlock x:Name="WatcherStatusText" Text="{Binding StatusText}" Foreground="{StaticResource Text1Brush}" FontSize="11" FontWeight="Bold"/>
                <ToggleButton x:Name="WatcherToggle" IsChecked="{Binding IsWatching, Mode=TwoWay}" Margin="0,6,0,0" Content="Toggle"/>
            </StackPanel>
        </Border>
    </StackPanel>

    <StackPanel>
        <TextBlock Text="PRIMEOS TUNER" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}" Margin="6,0,0,16"/>
        <TextBlock Text="NAVIGATION" Style="{StaticResource SectionLabel}" Margin="6,0,0,8"/>
        <Button Content="âŒ‚  Dashboard"  Tag="Dashboard"  Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
        <Button Content="âš¡  Optimize"   Tag="Optimize"   Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
        <Button Content="ðŸŽ®  Game Boost" Tag="GameBoost"  Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
        <Button Content="ðŸ“š  Library"    Tag="GameLibrary" Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
        <Button Content="âš™  Custom"     Tag="CustomMode" Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
        <Button Content="â›¨  History"    Tag="History"    Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
    </StackPanel>
</DockPanel>
```

(Replace the old `<StackPanel Margin="12,20">...</StackPanel>` block with this `<DockPanel>...`).

- [ ] **Step 3: Update `MainWindow.xaml.cs`** to set DataContext for the sidebar status section AND route the new tabs:

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm)
    {
        InitializeComponent();
        DataContext = vm;
        // Bind the bottom watcher block to its own VM
        var bottomBlock = (FrameworkElement)FindName("WatcherStatusText");
        if (bottomBlock?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

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
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "CustomMode"   => sp.GetRequiredService<CustomModeView>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
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

- [ ] **Step 4: Register in DI** — add to `App.xaml.cs`:

```csharp
s.AddSingleton<WatcherStatusViewModel>();
```

- [ ] **Step 5: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "Add sidebar watcher toggle and route Game Boost / Library / Custom Mode tabs"
```

---

### Task 40: Dashboard "Currently active profile" panel

**Files:**
- Modify: `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs`
- Modify: `src/PrimeOSTuner.UI/Views/DashboardView.xaml`

- [ ] **Step 1: Add active-profile properties to `DashboardViewModel.cs`** — append inside the class (no new class members other than these):

```csharp
[ObservableProperty] private string? _activeProfileName;
[ObservableProperty] private string? _activeGameName;
[ObservableProperty] private bool _hasActiveProfile;
```

- [ ] **Step 2: Add a periodic active-profile refresh** — add this constructor parameter and timer at class start. Replace the existing constructor with:

```csharp
private readonly ActiveTweaksStore _activeStore;
private readonly System.Timers.Timer _refreshTimer = new(2000) { AutoReset = true };

public DashboardViewModel(SystemSampler sampler, ActiveTweaksStore activeStore)
{
    _sampler = sampler;
    _activeStore = activeStore;
    _sampler.Sampled += OnSampled;
    _sampler.Start();
    _refreshTimer.Elapsed += async (_, _) => await RefreshActiveAsync();
    _refreshTimer.Start();
}

private async Task RefreshActiveAsync()
{
    var rec = await _activeStore.LoadAsync();
    var dispatcher = Application.Current?.Dispatcher;
    Action update = () =>
    {
        HasActiveProfile = rec is not null;
        ActiveProfileName = rec?.ProfileId;
        ActiveGameName = rec?.GameId;
    };
    if (dispatcher is null || dispatcher.CheckAccess()) update();
    else dispatcher.Invoke(update);
}
```

(Update existing `Dispose` to also stop `_refreshTimer`.)

- [ ] **Step 3: Add the panel to `DashboardView.xaml`** — insert as a new row above `<!-- Stats row -->` by changing the row definitions to add another auto row, and inserting:

```xml
<Border Grid.Row="2" Style="{StaticResource CardBorder}" Margin="0,0,0,18"
        Visibility="{Binding HasActiveProfile, Converter={StaticResource BoolToVisibility}}">
    <StackPanel>
        <TextBlock Text="ACTIVE PROFILE" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}"/>
        <TextBlock Foreground="{StaticResource Text0Brush}" FontSize="14" FontWeight="Bold">
            <Run Text="{Binding ActiveProfileName}"/>
            <Run Text=" applied for "/>
            <Run Text="{Binding ActiveGameName}"/>
        </TextBlock>
        <TextBlock Text="Tweaks will revert automatically when the game closes." Foreground="{StaticResource Text2Brush}" FontSize="11" Margin="0,4,0,0"/>
    </StackPanel>
</Border>
```

(Renumber the existing rows accordingly: stats row `Grid.Row="3"`, recent activity row `Grid.Row="4"`. Update the corresponding `<RowDefinition>`s to include one more `Auto` row.)

- [ ] **Step 4: Build**

```powershell
dotnet build src/PrimeOSTuner.UI
```

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "Add Currently Active Profile panel to dashboard"
```

---

### Task 41: VM smoke test

This is a manual end-to-end test, not a code task. No commit at the end unless changes are needed.

- [ ] **Step 1: Restore your VM to the `v02-baseline` snapshot**

In VirtualBox: select VM â†’ Snapshots â†’ right-click `v02-baseline` â†’ Restore.

- [ ] **Step 2: Build a single-file release**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2
```

- [ ] **Step 3: Copy `publish/v0.2` into the VM**

- [ ] **Step 4: Inside the VM, copy your settings.json** (with the SteamGridDB API key) to `%LOCALAPPDATA%\PrimeOSTuner\settings.json`

- [ ] **Step 5: Run `PrimeOSTuner.UI.exe`** in the VM

Expected: app opens; sidebar now has 6 nav items; bottom shows watcher status.

- [ ] **Step 6: Click Game Library**

Expected: Steam-installed games appear with cover art (if API key is set). VALORANT/League/Fortnite static entries appear without cover art unless found via search.

- [ ] **Step 7: Pick a profile from the dropdown of one game**

Expected: dropdown shows `(none)`, `basic`, `performance`, `custom`. Choose `basic`.

- [ ] **Step 8: Verify the assignment persists** — close and reopen the app

Expected: chosen game still shows `basic` as its mode.

- [ ] **Step 9: Click Game Boost â†’ Apply Basic Now**

Expected: status panel shows "Basic Mode: 4 applied, 0 failed." Verify in Task Manager / SystemPropertiesPerformance.exe that visual effects switched.

- [ ] **Step 10: Click Custom Mode â†’ tick `Disable mouse acceleration` â†’ Save**

Expected: dialog "Custom Mode saved."

- [ ] **Step 11: Click Game Boost â†’ Apply Custom**

Expected: status shows "Custom Mode: 1 applied".

- [ ] **Step 12: Open the assigned game** (e.g. launch Team Fortress 2 from Steam)

Expected: within ~2 seconds the dashboard's "Active Profile" panel appears showing `basic applied for steam.440`.

- [ ] **Step 13: Close the game**

Expected: within ~2 seconds the panel disappears, history tab shows revert entries.

- [ ] **Step 14: Test crash recovery**
  - Launch the assigned game again (panel appears)
  - Force-close PrimeOSTuner.UI from Task Manager (do NOT use the X button)
  - Restart `PrimeOSTuner.UI.exe`

Expected: at startup, the app silently reverts the active tweaks; no panel shows; `active-tweaks.json` is gone.

- [ ] **Step 15: Add a non-Steam game** — Game Library â†’ "+ Add Game" â†’ pick `notepad.exe` and name it `Notepad Demo` â†’ Add

Expected: a new card appears. Open notepad.exe, then close it; the lifecycle service reacts as before.

- [ ] **Step 16: Toggle the watcher off**

Expected: status text changes to "Watcher off"; opening a known game now does NOT trigger profile apply.

- [ ] **Step 17: Verify logs**

`%LOCALAPPDATA%\PrimeOSTuner\logs\primeos-YYYYMMDD.log` — no exceptions during any of the above.

- [ ] **Step 18: Restore the VM snapshot**

- [ ] **Step 19: If anything failed**, write a bug-fix task before continuing. Don't skip past failures.

---

### Task 42: Tag v0.2.0

- [ ] **Step 1: Tag the commit**

```powershell
git tag -a v0.2.0 -m "v0.2.0 — Game Boost + Mode Profiles + Game Library"
git log --oneline -1
```

Expected: shows the latest commit and the tag.

- [ ] **Step 2: Update memory**

Add a one-line entry to `C:\Users\jaxso\.claude\projects\C--Users-jaxso-projects-PC-Performance-booster\memory\MEMORY.md`:

```
- [Project: v0.2 shipped](project_primeos_tuner.md) — Game Boost + Mode Profiles + Game Library; v0.2.0 tagged 2026-MM-DD
```

(Or amend `project_primeos_tuner.md` "Status" section.)

---

## Done

You now have:
- 9 new game-focused tweaks (mouse accel, timer res, Game Mode, GPU scheduling, per-app GPU prefs, Nagle, network throttling, system responsiveness, CPU core parking)
- A 3-tier profile system (Basic / Performance / Custom) with persistent custom profile
- Steam library auto-discovery via VDF parsing
- SteamGridDB cover art with disk-cache and graceful no-key fallback
- A combined game registry (Steam + static catalog of 6 well-known non-Steam games + user-added)
- A 2-second-poll game watcher with auto-apply on launch / auto-revert on exit
- Crash-safe undo via `active-tweaks.json` recovery on startup
- Game Library UI, Custom Mode UI, Game Boost UI
- Sidebar watcher toggle and dashboard active-profile panel
- VM-tested release tagged v0.2.0

## Out of scope (deferred to v0.3+)

- **Real-time in-game FPS overlay** — explicitly deferred.
- **Code signing** — still requires a paid certificate; SmartScreen will warn.
- **Driver detection / installation / update** — not in this version.
- **Bloatware / telemetry tabs** — slated for v0.3 along with the Bloatware tab from the v1 spec.
- **Per-game tweak overrides** (e.g., "for Valorant only, also disable mouse smoothing") — out of scope; users assemble Custom Mode and apply.
- **Cloud sync of game-profile assignments** — out of scope.

**Next plan:** v0.3 — Bloatware/telemetry tab + FPS overlay + driver scan. Will be drafted when v0.2 is fully shipped and you've confirmed it works in your environment.

