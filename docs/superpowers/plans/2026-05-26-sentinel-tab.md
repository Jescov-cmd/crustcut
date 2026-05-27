# Sentinel Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a passive performance-watcher tab ("Sentinel") that compares a running Library-game's live VRAM / RAM / CPU usage against the game's Steam-listed minimum / recommended specs and surfaces a subtle red dot on the nav tab when something looks wrong.

**Architecture:** Three pure data layers (parser, detection rules, snapshot record) + two side-effecting providers (HTTP spec fetcher with on-disk cache, Win11 perf-counter sampler) + one stateful orchestrator (`SentinelService`) that the existing `ProfileLifecycleService` calls on `GameStarted` / `GameStopped`. UI is a tab + a VM bound to the orchestrator's `Changed` event, plus a red-dot overlay on the existing nav strip.

**Tech Stack:** .NET 9 / C# / WPF, xUnit + FluentAssertions + Moq, `System.Net.Http`, Win11 `\GPU Engine` and `\GPU Adapter Memory` performance counters, existing CommunityToolkit.Mvvm + Microsoft.Extensions.Hosting DI.

**Spec:** [docs/superpowers/specs/2026-05-26-sentinel-tab-design.md](../specs/2026-05-26-sentinel-tab-design.md)

---

## File map

**Create:**

- `src/PrimeOSTuner.Core/Sentinel/SteamPcRequirements.cs` — parsed-spec record
- `src/PrimeOSTuner.Core/Sentinel/SteamSpecParser.cs` — pure HTML→record parser
- `src/PrimeOSTuner.Core/Sentinel/Problem.cs` — `Problem` record + `ProblemKind` enum
- `src/PrimeOSTuner.Core/Sentinel/MetricsSnapshot.cs` — sample record
- `src/PrimeOSTuner.Core/Sentinel/DetectionRules.cs` — pure rule evaluator
- `src/PrimeOSTuner.Core/Sentinel/ISpecFetcher.cs` — interface
- `src/PrimeOSTuner.Core/Sentinel/SteamSpecFetcher.cs` — HTTP + disk-cache impl
- `src/PrimeOSTuner.Core/Sentinel/IMetricsSampler.cs` — interface
- `src/PrimeOSTuner.Core/Sentinel/ISentinelService.cs` — interface
- `src/PrimeOSTuner.Core/Sentinel/SentinelService.cs` — orchestration
- `src/PrimeOSTuner.Win/Sentinel/GpuPerfCounterMetricsSampler.cs` — Win11 perf-counter sampler
- `src/PrimeOSTuner.UI/ViewModels/SentinelViewModel.cs`
- `src/PrimeOSTuner.UI/ViewModels/SentinelRowViewModel.cs` — per-axis row VM
- `src/PrimeOSTuner.UI/Views/SentinelView.xaml`
- `src/PrimeOSTuner.UI/Views/SentinelView.xaml.cs`
- `src/PrimeOSTuner.Tests/Sentinel/SteamSpecParserTests.cs`
- `src/PrimeOSTuner.Tests/Sentinel/DetectionRulesTests.cs`
- `src/PrimeOSTuner.Tests/Sentinel/SentinelServiceTests.cs`
- `src/PrimeOSTuner.Tests/Fixtures/steam-specs/valve-style.html`
- `src/PrimeOSTuner.Tests/Fixtures/steam-specs/bethesda-style.html`
- `src/PrimeOSTuner.Tests/Fixtures/steam-specs/indie-minimal.html`

**Modify:**

- `src/PrimeOSTuner.Core/Settings/AppSettings.cs` — add `SentinelEnabled`
- `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs` — wire `ISentinelService`
- `src/PrimeOSTuner.UI/App.xaml.cs` — DI for everything
- `src/PrimeOSTuner.UI/MainWindow.xaml` — Sentinel nav tab + red-dot overlay
- `src/PrimeOSTuner.UI/MainWindow.xaml.cs` — Sentinel case in tab dispatch
- `src/PrimeOSTuner.UI/Theme/Icons.xaml` — `IconSentinel`
- `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj` — include fixture HTML files

---

## Task 1: SteamSpecParser

Pure parser that turns Steam's `pc_requirements` HTML blob into a typed record. Tolerant — missing fields stay `null`.

**Files:**
- Create: `src/PrimeOSTuner.Core/Sentinel/SteamPcRequirements.cs`
- Create: `src/PrimeOSTuner.Core/Sentinel/SteamSpecParser.cs`
- Create: `src/PrimeOSTuner.Tests/Sentinel/SteamSpecParserTests.cs`
- Create: `src/PrimeOSTuner.Tests/Fixtures/steam-specs/valve-style.html`
- Create: `src/PrimeOSTuner.Tests/Fixtures/steam-specs/bethesda-style.html`
- Create: `src/PrimeOSTuner.Tests/Fixtures/steam-specs/indie-minimal.html`
- Modify: `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj` — copy fixtures to test output

- [ ] **Step 1.1: Create the SteamPcRequirements record**

`src/PrimeOSTuner.Core/Sentinel/SteamPcRequirements.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Parsed Steam PC requirements. Nullable fields = the parser could not
/// extract that value from the spec HTML. Treat null as "unknown" — never
/// fire a detection rule against an unknown value.
/// </summary>
public sealed record SteamPcRequirements(
    int? MinRamMb,
    int? RecRamMb,
    int? MinVramMb,
    int? RecVramMb);
```

- [ ] **Step 1.2: Create the three HTML fixtures**

`src/PrimeOSTuner.Tests/Fixtures/steam-specs/valve-style.html`:

```html
<strong>Minimum:</strong><br><ul class="bb_ul"><li><strong>OS:</strong> Windows 10 64-bit<br></li><li><strong>Processor:</strong> Intel Core i5-3470<br></li><li><strong>Memory:</strong> 8 GB RAM<br></li><li><strong>Graphics:</strong> NVIDIA GeForce GTX 780 3GB<br></li><li><strong>Storage:</strong> 70 GB available space</li></ul>
```

`src/PrimeOSTuner.Tests/Fixtures/steam-specs/bethesda-style.html`:

```html
<strong>Recommended:</strong><br><ul class="bb_ul"><li><strong>OS *:</strong> Windows 10/11 64-bit<br></li><li><strong>Processor:</strong> AMD Ryzen 5 3600X<br></li><li><strong>Memory:</strong> 16GB RAM<br></li><li><strong>Graphics:</strong> NVIDIA GeForce RTX 2060 Super 8 GB<br></li><li><strong>Storage:</strong> 125 GB available space</li></ul>
```

`src/PrimeOSTuner.Tests/Fixtures/steam-specs/indie-minimal.html`:

```html
<strong>Minimum:</strong><br><ul class="bb_ul"><li><strong>OS:</strong> Windows 10<br></li><li><strong>Memory:</strong> 4 GB RAM<br></li><li><strong>Storage:</strong> 2 GB available space</li></ul>
```

- [ ] **Step 1.3: Mark fixtures as content copied to test output**

Edit `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj` and add **inside the existing `<Project>` element, after the last `<ItemGroup>`**:

```xml
<ItemGroup>
  <None Update="Fixtures\steam-specs\*.html">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 1.4: Write the failing parser tests**

`src/PrimeOSTuner.Tests/Sentinel/SteamSpecParserTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class SteamSpecParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-specs", name);

    [Fact]
    public void Valve_style_minimum_extracts_8GB_ram_and_3GB_vram()
    {
        var html = File.ReadAllText(FixturePath("valve-style.html"));

        var spec = SteamSpecParser.ParseMinimum(html);

        spec.MinRamMb.Should().Be(8192);
        spec.MinVramMb.Should().Be(3072);
    }

    [Fact]
    public void Bethesda_style_recommended_extracts_16GB_ram_and_8GB_vram()
    {
        var html = File.ReadAllText(FixturePath("bethesda-style.html"));

        var spec = SteamSpecParser.ParseRecommended(html);

        spec.RecRamMb.Should().Be(16384);
        spec.RecVramMb.Should().Be(8192);
    }

    [Fact]
    public void Indie_minimal_extracts_4GB_ram_and_leaves_vram_null()
    {
        var html = File.ReadAllText(FixturePath("indie-minimal.html"));

        var spec = SteamSpecParser.ParseMinimum(html);

        spec.MinRamMb.Should().Be(4096);
        spec.MinVramMb.Should().BeNull();
    }

    [Fact]
    public void Empty_html_returns_all_nulls()
    {
        var spec = SteamSpecParser.ParseMinimum("");

        spec.MinRamMb.Should().BeNull();
        spec.MinVramMb.Should().BeNull();
    }

    [Fact]
    public void Garbage_html_does_not_throw_and_returns_all_nulls()
    {
        var spec = SteamSpecParser.ParseMinimum("<not><real>html");

        spec.MinRamMb.Should().BeNull();
        spec.MinVramMb.Should().BeNull();
    }

    [Theory]
    [InlineData("8 GB",  8192)]
    [InlineData("8GB",   8192)]
    [InlineData("16 gb", 16384)]
    [InlineData("16 Gigabytes", 16384)]
    public void ParseSizeMb_handles_common_size_spellings(string input, int expectedMb)
    {
        SteamSpecParser.ParseSizeMb(input).Should().Be(expectedMb);
    }
}
```

- [ ] **Step 1.5: Run the tests to verify they fail**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter SteamSpecParserTests --nologo -v minimal
```

Expected: build failure ("`SteamSpecParser` does not exist").

- [ ] **Step 1.6: Implement the parser**

`src/PrimeOSTuner.Core/Sentinel/SteamSpecParser.cs`:

```csharp
using System.Text.RegularExpressions;

namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Pure parser for the HTML blobs Steam returns under
/// <c>pc_requirements.minimum</c> / <c>pc_requirements.recommended</c>.
/// Tolerant by design: any field we cannot extract stays null so the
/// detection rules silently skip that axis instead of firing on bad data.
/// </summary>
public static class SteamSpecParser
{
    /// <summary>Parse the "minimum" HTML blob.</summary>
    public static SteamPcRequirements ParseMinimum(string html)
        => new(MinRamMb: ExtractRamMb(html), RecRamMb: null,
               MinVramMb: ExtractVramMb(html), RecVramMb: null);

    /// <summary>Parse the "recommended" HTML blob.</summary>
    public static SteamPcRequirements ParseRecommended(string html)
        => new(MinRamMb: null, RecRamMb: ExtractRamMb(html),
               MinVramMb: null, RecVramMb: ExtractVramMb(html));

    /// <summary>Combine a min spec and a rec spec into a single record.</summary>
    public static SteamPcRequirements Merge(SteamPcRequirements min, SteamPcRequirements rec)
        => new(MinRamMb: min.MinRamMb, RecRamMb: rec.RecRamMb,
               MinVramMb: min.MinVramMb, RecVramMb: rec.RecVramMb);

    /// <summary>Parse a size string like "8 GB" / "8GB" / "16 gigabytes" into MB.</summary>
    public static int? ParseSizeMb(string text)
    {
        var m = Regex.Match(text, @"(\d+)\s*(gb|gigabytes?)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var gb)) return null;
        return gb * 1024;
    }

    // RAM lives on a "Memory:" line.
    private static int? ExtractRamMb(string html)
    {
        var m = Regex.Match(html,
            @"Memory:?\s*</strong>?\s*([^<]+)",
            RegexOptions.IgnoreCase);
        return m.Success ? ParseSizeMb(m.Groups[1].Value) : null;
    }

    // VRAM is usually a "Graphics:" line ending in "<size> GB".
    private static int? ExtractVramMb(string html)
    {
        var m = Regex.Match(html,
            @"Graphics:?\s*</strong>?\s*([^<]+)",
            RegexOptions.IgnoreCase);
        return m.Success ? ParseSizeMb(m.Groups[1].Value) : null;
    }
}
```

- [ ] **Step 1.7: Run the tests to verify they pass**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter SteamSpecParserTests --nologo -v minimal
```

Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 1.8: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/SteamPcRequirements.cs `
        src/PrimeOSTuner.Core/Sentinel/SteamSpecParser.cs `
        src/PrimeOSTuner.Tests/Sentinel/SteamSpecParserTests.cs `
        src/PrimeOSTuner.Tests/Fixtures/steam-specs/ `
        src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj

git commit -m "feat(sentinel): tolerant Steam spec HTML parser"
```

---

## Task 2: DetectionRules + supporting records

Pure rule evaluator over a `MetricsSnapshot` + a `SteamPcRequirements` + the system's hardware capacities. No I/O. Each rule returns zero or more `Problem`s.

**Files:**
- Create: `src/PrimeOSTuner.Core/Sentinel/Problem.cs`
- Create: `src/PrimeOSTuner.Core/Sentinel/MetricsSnapshot.cs`
- Create: `src/PrimeOSTuner.Core/Sentinel/DetectionRules.cs`
- Create: `src/PrimeOSTuner.Tests/Sentinel/DetectionRulesTests.cs`

- [ ] **Step 2.1: Create the Problem record + enum**

`src/PrimeOSTuner.Core/Sentinel/Problem.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

public enum ProblemKind
{
    VramOverhead,
    RamPressure,
    CpuSaturated,
}

public sealed record Problem(ProblemKind Kind, string Detail, DateTime DetectedAt);
```

- [ ] **Step 2.2: Create the MetricsSnapshot record**

`src/PrimeOSTuner.Core/Sentinel/MetricsSnapshot.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// A single point-in-time snapshot of system + game-process resource use.
/// Negative values (-1) mean "the sampler could not read this metric" — rules
/// treat that as unknown and stay silent.
/// </summary>
public sealed record MetricsSnapshot(
    DateTime At,
    int GamePid,
    double SystemCpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long VramUsedBytes,
    long VramTotalBytes);
```

- [ ] **Step 2.3: Write the failing detection-rule tests**

`src/PrimeOSTuner.Tests/Sentinel/DetectionRulesTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class DetectionRulesTests
{
    private static readonly DateTime Now = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private static MetricsSnapshot Snap(
        double cpu = 10, long ramUsed = 4L * 1024 * 1024 * 1024, long ramTotal = 16L * 1024 * 1024 * 1024,
        long vramUsed = 2L * 1024 * 1024 * 1024, long vramTotal = 12L * 1024 * 1024 * 1024)
        => new(Now, GamePid: 1234, cpu, ramUsed, ramTotal, vramUsed, vramTotal);

    [Fact]
    public void Vram_overhead_fires_when_usage_is_high_and_game_only_needs_a_little()
    {
        // 11.5 GB of 12 GB used, game's recommended is 4 GB.
        var snap = Snap(vramUsed: 11_500L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 4096);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_does_not_fire_when_game_actually_needs_lots_of_vram()
    {
        // 11.5 GB of 12 GB used, but game's recommended is 10 GB — expected.
        var snap = Snap(vramUsed: 11_500L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 10_240);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_stays_silent_when_RecVramMb_is_unknown()
    {
        var snap = Snap(vramUsed: 11_500L * 1024 * 1024, vramTotal: 12L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Vram_overhead_stays_silent_when_sampler_reports_unknown_vram()
    {
        var snap = Snap(vramUsed: -1, vramTotal: -1);
        var spec = new SteamPcRequirements(null, null, null, RecVramMb: 4096);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.VramOverhead);
    }

    [Fact]
    public void Ram_pressure_fires_when_usage_is_high_and_game_only_needs_a_little()
    {
        var snap = Snap(ramUsed: 15_500L * 1024 * 1024, ramTotal: 16L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, RecRamMb: 8192, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Ram_pressure_does_not_fire_when_game_legitimately_needs_lots_of_ram()
    {
        var snap = Snap(ramUsed: 15_500L * 1024 * 1024, ramTotal: 16L * 1024 * 1024 * 1024);
        var spec = new SteamPcRequirements(null, RecRamMb: 16_384, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, rollingCpuWindow: new());

        problems.Should().NotContain(p => p.Kind == ProblemKind.RamPressure);
    }

    [Fact]
    public void Cpu_saturated_fires_when_all_samples_in_30s_window_exceed_90_percent()
    {
        // Eight consecutive 91% samples over the last 30s.
        var window = new Queue<(DateTime, double)>();
        for (int i = 7; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 91.0));

        var snap = Snap(cpu: 91);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().ContainSingle(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Cpu_saturated_does_not_fire_when_one_sample_dipped_below_90()
    {
        var window = new Queue<(DateTime, double)>();
        for (int i = 7; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), i == 3 ? 50.0 : 91.0));

        var snap = Snap(cpu: 91);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public void Cpu_saturated_does_not_fire_when_window_is_too_short()
    {
        // Only 3 samples — not yet 30s of data.
        var window = new Queue<(DateTime, double)>();
        for (int i = 2; i >= 0; i--)
            window.Enqueue((Now.AddSeconds(-i * 4), 99.0));

        var snap = Snap(cpu: 99);
        var spec = new SteamPcRequirements(null, null, null, null);

        var problems = DetectionRules.Evaluate(snap, spec, window);

        problems.Should().NotContain(p => p.Kind == ProblemKind.CpuSaturated);
    }
}
```

- [ ] **Step 2.4: Run the tests to verify they fail**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter DetectionRulesTests --nologo -v minimal
```

Expected: build failure ("`DetectionRules` does not exist").

- [ ] **Step 2.5: Implement DetectionRules**

`src/PrimeOSTuner.Core/Sentinel/DetectionRules.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Pure detection rules. Given a snapshot, the parsed spec, and a rolling
/// CPU history, return the list of currently-active problems.
///
/// "Silent on uncertainty" is the prime directive — when a field is null
/// (unknown spec) or negative (unknown sample), the affected rule does
/// nothing. Never a false alarm.
/// </summary>
public static class DetectionRules
{
    // VRAM rule: usage > 95% of card AND game's recommended <= 50% of card.
    private const double VramHighWatermark = 0.95;
    private const double VramRecCardRatio  = 0.50;

    // RAM rule: usage > 95% of total AND game's recommended <= 75% of total.
    private const double RamHighWatermark = 0.95;
    private const double RamRecTotalRatio = 0.75;

    // CPU rule: every sample in trailing 30s > 90%.
    private const double CpuHighWatermark = 90.0;
    private static readonly TimeSpan CpuWindow = TimeSpan.FromSeconds(30);

    public static IReadOnlyList<Problem> Evaluate(
        MetricsSnapshot snap,
        SteamPcRequirements spec,
        Queue<(DateTime At, double Percent)> rollingCpuWindow)
    {
        var problems = new List<Problem>();

        if (TryVramOverhead(snap, spec, out var vram)) problems.Add(vram!);
        if (TryRamPressure(snap, spec, out var ram))   problems.Add(ram!);
        if (TryCpuSaturated(snap, rollingCpuWindow, out var cpu)) problems.Add(cpu!);

        return problems;
    }

    private static bool TryVramOverhead(MetricsSnapshot snap, SteamPcRequirements spec, out Problem? p)
    {
        p = null;
        if (snap.VramUsedBytes < 0 || snap.VramTotalBytes <= 0) return false;   // unknown sample
        if (spec.RecVramMb is not int recMb) return false;                       // unknown spec

        var cardMb = (int)(snap.VramTotalBytes / 1024 / 1024);
        var usedMb = (int)(snap.VramUsedBytes / 1024 / 1024);

        var usageRatio = (double)usedMb / cardMb;
        var recRatio   = (double)recMb / cardMb;

        if (usageRatio <= VramHighWatermark) return false;
        if (recRatio   >  VramRecCardRatio)  return false;

        p = new Problem(
            ProblemKind.VramOverhead,
            $"VRAM is {usedMb} MB of {cardMb} MB — game's recommended is only {recMb} MB.",
            snap.At);
        return true;
    }

    private static bool TryRamPressure(MetricsSnapshot snap, SteamPcRequirements spec, out Problem? p)
    {
        p = null;
        if (snap.RamUsedBytes < 0 || snap.RamTotalBytes <= 0) return false;
        if (spec.RecRamMb is not int recMb) return false;

        var totalMb = (int)(snap.RamTotalBytes / 1024 / 1024);
        var usedMb  = (int)(snap.RamUsedBytes / 1024 / 1024);

        var usageRatio = (double)usedMb / totalMb;
        var recRatio   = (double)recMb / totalMb;

        if (usageRatio <= RamHighWatermark) return false;
        if (recRatio   >  RamRecTotalRatio) return false;

        p = new Problem(
            ProblemKind.RamPressure,
            $"System RAM is {usedMb} MB of {totalMb} MB — game's recommended is only {recMb} MB.",
            snap.At);
        return true;
    }

    private static bool TryCpuSaturated(
        MetricsSnapshot snap,
        Queue<(DateTime At, double Percent)> window,
        out Problem? p)
    {
        p = null;
        if (snap.SystemCpuPercent < 0) return false;
        if (window.Count == 0) return false;

        // Need a full 30s of history.
        var span = snap.At - window.Peek().At;
        if (span < CpuWindow) return false;

        // Every sample in the window AND the current snapshot must exceed the watermark.
        if (snap.SystemCpuPercent <= CpuHighWatermark) return false;
        if (window.Any(s => s.Percent <= CpuHighWatermark)) return false;

        p = new Problem(
            ProblemKind.CpuSaturated,
            $"System CPU has been above {CpuHighWatermark:F0}% for the last {CpuWindow.TotalSeconds:F0} s.",
            snap.At);
        return true;
    }
}
```

- [ ] **Step 2.6: Run the tests to verify they pass**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter DetectionRulesTests --nologo -v minimal
```

Expected: `Passed: 9, Failed: 0`.

- [ ] **Step 2.7: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/Problem.cs `
        src/PrimeOSTuner.Core/Sentinel/MetricsSnapshot.cs `
        src/PrimeOSTuner.Core/Sentinel/DetectionRules.cs `
        src/PrimeOSTuner.Tests/Sentinel/DetectionRulesTests.cs

git commit -m "feat(sentinel): pure detection rules — VRAM / RAM / sustained CPU"
```

---

## Task 3: ISpecFetcher + SteamSpecFetcher (HTTP + disk cache)

Concrete fetcher that hits Steam's appdetails endpoint, parses both `pc_requirements.minimum` and `pc_requirements.recommended`, merges them, and caches the result on disk. All failures swallow to `null`.

**Files:**
- Create: `src/PrimeOSTuner.Core/Sentinel/ISpecFetcher.cs`
- Create: `src/PrimeOSTuner.Core/Sentinel/SteamSpecFetcher.cs`

- [ ] **Step 3.1: Create the interface**

`src/PrimeOSTuner.Core/Sentinel/ISpecFetcher.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

public interface ISpecFetcher
{
    /// <summary>
    /// Returns the parsed Steam spec for a Steam app id. Returns null on any
    /// failure — network, parse, missing data. Implementations should cache
    /// results so a fresh app launch doesn't re-hit Steam for known games.
    /// </summary>
    Task<SteamPcRequirements?> FetchAsync(string steamAppId, CancellationToken ct = default);
}
```

- [ ] **Step 3.2: Implement SteamSpecFetcher**

`src/PrimeOSTuner.Core/Sentinel/SteamSpecFetcher.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeOSTuner.Core.Sentinel;

public sealed class SteamSpecFetcher : ISpecFetcher
{
    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly Dictionary<string, SteamPcRequirements> _cache = new();
    private readonly object _gate = new();

    public SteamSpecFetcher(HttpClient http, string? cachePath = null)
    {
        _http = http;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://store.steampowered.com");
        _cachePath = cachePath ?? DefaultCachePath();
        LoadCacheFromDisk();
    }

    public static string DefaultCachePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "sentinel-specs.json");

    public async Task<SteamPcRequirements?> FetchAsync(string steamAppId, CancellationToken ct = default)
    {
        lock (_gate) if (_cache.TryGetValue(steamAppId, out var hit)) return hit;

        try
        {
            var resp = await _http.GetFromJsonAsync<Dictionary<string, AppDetailsEnvelope>>(
                $"/api/appdetails?appids={steamAppId}&filters=basic", ct);
            if (resp is null || !resp.TryGetValue(steamAppId, out var env) || !env.Success || env.Data is null)
                return null;

            var minHtml = env.Data.PcRequirements?.Minimum ?? "";
            var recHtml = env.Data.PcRequirements?.Recommended ?? "";

            var min = SteamSpecParser.ParseMinimum(minHtml);
            var rec = SteamSpecParser.ParseRecommended(recHtml);
            var merged = SteamSpecParser.Merge(min, rec);

            lock (_gate) _cache[steamAppId] = merged;
            SaveCacheToDisk();
            return merged;
        }
        catch
        {
            return null;
        }
    }

    private void LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            var json = File.ReadAllText(_cachePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, SteamPcRequirements>>(json);
            if (loaded is null) return;
            lock (_gate) foreach (var kv in loaded) _cache[kv.Key] = kv.Value;
        }
        catch { /* corrupt cache → just start fresh */ }
    }

    private void SaveCacheToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            Dictionary<string, SteamPcRequirements> snapshot;
            lock (_gate) snapshot = new Dictionary<string, SteamPcRequirements>(_cache);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(snapshot));
        }
        catch { /* cache writes are best-effort */ }
    }

    private sealed class AppDetailsEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("data")] public AppDetailsData? Data { get; set; }
    }

    private sealed class AppDetailsData
    {
        [JsonPropertyName("pc_requirements")] public PcRequirementsBlob? PcRequirements { get; set; }
    }

    private sealed class PcRequirementsBlob
    {
        [JsonPropertyName("minimum")] public string? Minimum { get; set; }
        [JsonPropertyName("recommended")] public string? Recommended { get; set; }
    }
}
```

- [ ] **Step 3.3: Verify it builds**

Run:

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3.4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/ISpecFetcher.cs `
        src/PrimeOSTuner.Core/Sentinel/SteamSpecFetcher.cs

git commit -m "feat(sentinel): Steam spec fetcher with disk cache"
```

---

## Task 4: IMetricsSampler + GpuPerfCounterMetricsSampler

Win11-only sampler. Reads system CPU, system RAM via `GlobalMemoryStatusEx`, and per-adapter GPU memory via the `\GPU Adapter Memory(*)\Dedicated Usage` counter. Returns `-1` for any value it can't read so detection rules degrade silently.

**Files:**
- Create: `src/PrimeOSTuner.Core/Sentinel/IMetricsSampler.cs`
- Create: `src/PrimeOSTuner.Win/Sentinel/GpuPerfCounterMetricsSampler.cs`

- [ ] **Step 4.1: Create the interface**

`src/PrimeOSTuner.Core/Sentinel/IMetricsSampler.cs`:

```csharp
namespace PrimeOSTuner.Core.Sentinel;

public interface IMetricsSampler
{
    /// <summary>
    /// Take one snapshot. Implementations may block briefly on perf counters.
    /// Any value the sampler cannot read should come back as -1 so the
    /// detection rules can treat it as "unknown" instead of "zero."
    /// </summary>
    Task<MetricsSnapshot> SampleAsync(int gamePid, CancellationToken ct = default);
}
```

- [ ] **Step 4.2: Create GpuPerfCounterMetricsSampler**

`src/PrimeOSTuner.Win/Sentinel/GpuPerfCounterMetricsSampler.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using PrimeOSTuner.Core.Sentinel;

namespace PrimeOSTuner.Win.Sentinel;

/// <summary>
/// Win11-friendly metrics sampler. Reads system CPU via PerformanceCounter,
/// system RAM via GlobalMemoryStatusEx, and dedicated GPU memory via the
/// <c>\GPU Adapter Memory(*)\Dedicated Usage</c> counter (cross-vendor on
/// modern Windows).
/// </summary>
public sealed class GpuPerfCounterMetricsSampler : IMetricsSampler, IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private bool _cpuPrimed;

    public GpuPerfCounterMetricsSampler()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
    }

    public async Task<MetricsSnapshot> SampleAsync(int gamePid, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // PerformanceCounter's first read is always 0 — prime it once.
        double cpu;
        if (!_cpuPrimed)
        {
            _cpuCounter.NextValue();
            await Task.Delay(120, ct);
            _cpuPrimed = true;
        }
        try { cpu = _cpuCounter.NextValue(); }
        catch { cpu = -1; }

        var (ramUsed, ramTotal) = ReadSystemRam();
        var (vramUsed, vramTotal) = ReadDedicatedGpuMemory();

        return new MetricsSnapshot(now, gamePid, cpu, ramUsed, ramTotal, vramUsed, vramTotal);
    }

    private static (long Used, long Total) ReadSystemRam()
    {
        var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref s)) return (-1, -1);
        return ((long)(s.ullTotalPhys - s.ullAvailPhys), (long)s.ullTotalPhys);
    }

    private static (long Used, long Total) ReadDedicatedGpuMemory()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var instances = category.GetInstanceNames();
            if (instances.Length == 0) return (-1, -1);

            long used = 0;
            long total = 0;
            foreach (var inst in instances)
            {
                using var usage = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", inst, readOnly: true);
                used += (long)usage.NextValue();

                // "Dedicated Limit" is the per-adapter capacity. Newer Windows builds
                // expose it; older builds only have Usage.
                try
                {
                    using var limit = new PerformanceCounter("GPU Adapter Memory", "Dedicated Limit", inst, readOnly: true);
                    total += (long)limit.NextValue();
                }
                catch { /* leave total at 0 — caller treats <=0 as unknown */ }
            }
            return (used, total > 0 ? total : -1);
        }
        catch
        {
            return (-1, -1);
        }
    }

    public void Dispose() => _cpuCounter.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
```

- [ ] **Step 4.3: Verify it builds**

Run:

```powershell
dotnet build src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj --nologo -v minimal
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4.4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/IMetricsSampler.cs `
        src/PrimeOSTuner.Win/Sentinel/GpuPerfCounterMetricsSampler.cs

git commit -m "feat(sentinel): Win11 perf-counter metrics sampler"
```

---

## Task 5: SentinelService orchestration

Owns the sample loop, the rolling CPU window, the cached spec lookup, and the public `Currently` / `Changed` API. Wired to `ProfileLifecycleService` in Task 7.

**Files:**
- Create: `src/PrimeOSTuner.Core/Sentinel/ISentinelService.cs`
- Create: `src/PrimeOSTuner.Core/Sentinel/SentinelService.cs`
- Create: `src/PrimeOSTuner.Tests/Sentinel/SentinelServiceTests.cs`

- [ ] **Step 5.1: Create the interface**

`src/PrimeOSTuner.Core/Sentinel/ISentinelService.cs`:

```csharp
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Sentinel;

public interface ISentinelService
{
    /// <summary>Problems currently detected; empty when no game is running or no rule fires.</summary>
    IReadOnlyList<Problem> Currently { get; }

    /// <summary>Display name of the game being watched, or null when idle.</summary>
    string? WatchingGame { get; }

    /// <summary>Most recent metrics sample, or null if the loop hasn't ticked yet.</summary>
    MetricsSnapshot? LatestSnapshot { get; }

    /// <summary>Spec for the currently-watched game, or null if not yet fetched / unavailable.</summary>
    SteamPcRequirements? CurrentSpec { get; }

    /// <summary>Master switch. When false, OnGameStarted no-ops and any in-flight loop stops on next tick.</summary>
    bool Enabled { get; set; }

    /// <summary>Raised whenever Currently, WatchingGame, LatestSnapshot, or CurrentSpec changes.</summary>
    event EventHandler? Changed;

    /// <summary>Start watching a game. Idempotent — safe to call multiple times.</summary>
    void OnGameStarted(KnownGame game, int pid);

    /// <summary>Stop watching. Safe to call when not watching.</summary>
    void OnGameStopped();
}
```

- [ ] **Step 5.2: Write the failing service tests**

`src/PrimeOSTuner.Tests/Sentinel/SentinelServiceTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class SentinelServiceTests
{
    private static KnownGame Game(string id = "g1", string? appId = "1091500") =>
        new(id, "Test Game", new[] { "test.exe" }, appId, "C:\\Games\\Test", KnownGameSource.Steam);

    private static MetricsSnapshot Snap(DateTime at, double cpu = 10,
        long vramUsed = 2L * 1024 * 1024 * 1024, long vramTotal = 12L * 1024 * 1024 * 1024,
        long ramUsed = 4L * 1024 * 1024 * 1024, long ramTotal = 16L * 1024 * 1024 * 1024)
        => new(at, 1234, cpu, ramUsed, ramTotal, vramUsed, vramTotal);

    [Fact]
    public async Task OnGameStarted_fetches_spec_and_starts_publishing_problems()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync("1091500", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, RecVramMb: 4096));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, vramUsed: 11_500L * 1024 * 1024));

        var service = new SentinelService(fetcher.Object, sampler.Object, () => time);
        var changed = false;
        service.Changed += (_, _) => changed = true;

        service.OnGameStarted(Game(), pid: 1234);
        await service.TickOnceAsync();

        service.WatchingGame.Should().Be("Test Game");
        service.Currently.Should().ContainSingle(p => p.Kind == ProblemKind.VramOverhead);
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task OnGameStopped_clears_state_and_raises_Changed()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, RecVramMb: 4096));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, vramUsed: 11_500L * 1024 * 1024));

        var service = new SentinelService(fetcher.Object, sampler.Object, () => time);
        service.OnGameStarted(Game(), pid: 1234);
        await service.TickOnceAsync();

        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;
        service.OnGameStopped();

        service.WatchingGame.Should().BeNull();
        service.Currently.Should().BeEmpty();
        changedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OnGameStarted_with_no_app_id_skips_spec_fetch_but_still_runs_cpu_rule()
    {
        var fetcher = new Mock<ISpecFetcher>(MockBehavior.Strict); // any call would throw
        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

        var sampler = new Mock<IMetricsSampler>();
        var ticks = 0;
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time.AddSeconds(ticks++ * 4), cpu: 95));

        var service = new SentinelService(fetcher.Object, sampler.Object, () => time.AddSeconds(ticks * 4));

        service.OnGameStarted(Game(appId: null), pid: 1234);

        // 8 ticks at 4 s each = 32 s of sustained 95% CPU → CpuSaturated should fire.
        for (int i = 0; i < 8; i++) await service.TickOnceAsync();

        service.Currently.Should().ContainSingle(p => p.Kind == ProblemKind.CpuSaturated);
    }

    [Fact]
    public async Task Changed_fires_every_tick_because_LatestSnapshot_is_always_fresh()
    {
        var fetcher = new Mock<ISpecFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SteamPcRequirements(null, null, null, null));

        var time = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var sampler = new Mock<IMetricsSampler>();
        sampler.Setup(s => s.SampleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => Snap(time, cpu: 10));

        var service = new SentinelService(fetcher.Object, sampler.Object, () => time);
        service.OnGameStarted(Game(), pid: 1234);
        await service.TickOnceAsync();

        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;

        await service.TickOnceAsync();
        await service.TickOnceAsync();

        changedCount.Should().Be(2);   // every tick raises Changed so the VM can refresh live values
    }

    [Fact]
    public void Disabling_clears_state_and_subsequent_OnGameStarted_is_a_no_op()
    {
        var fetcher = new Mock<ISpecFetcher>();
        var sampler = new Mock<IMetricsSampler>();
        var service = new SentinelService(fetcher.Object, sampler.Object,
                                          () => new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc));
        service.OnGameStarted(Game(), pid: 1234);
        service.WatchingGame.Should().NotBeNull();

        service.Enabled = false;
        service.WatchingGame.Should().BeNull();

        service.OnGameStarted(Game(), pid: 1234);
        service.WatchingGame.Should().BeNull();
    }
}
```

- [ ] **Step 5.3: Run the tests to verify they fail**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter SentinelServiceTests --nologo -v minimal
```

Expected: build failure ("`SentinelService` does not exist").

- [ ] **Step 5.4: Implement SentinelService**

`src/PrimeOSTuner.Core/Sentinel/SentinelService.cs`:

```csharp
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Default <see cref="ISentinelService"/>. Owns the per-tick sample loop, the
/// rolling CPU history, and the current problem set. Time provider is
/// injectable so tests can drive sustained-CPU rules deterministically.
/// </summary>
public sealed class SentinelService : ISentinelService
{
    private readonly ISpecFetcher _specs;
    private readonly IMetricsSampler _sampler;
    private readonly Func<DateTime> _now;

    private readonly object _gate = new();
    private readonly Queue<(DateTime At, double Percent)> _cpuWindow = new();
    private SteamPcRequirements? _spec;
    private MetricsSnapshot? _latestSnapshot;
    private int _pid;
    private string? _watchingGame;
    private IReadOnlyList<Problem> _currently = Array.Empty<Problem>();
    private bool _enabled = true;

    private static readonly TimeSpan CpuWindowSize = TimeSpan.FromSeconds(30);

    public SentinelService(ISpecFetcher specs, IMetricsSampler sampler, Func<DateTime>? now = null)
    {
        _specs = specs;
        _sampler = sampler;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public IReadOnlyList<Problem> Currently { get { lock (_gate) return _currently; } }
    public string? WatchingGame { get { lock (_gate) return _watchingGame; } }
    public MetricsSnapshot? LatestSnapshot { get { lock (_gate) return _latestSnapshot; } }
    public SteamPcRequirements? CurrentSpec { get { lock (_gate) return _spec; } }

    public bool Enabled
    {
        get { lock (_gate) return _enabled; }
        set
        {
            bool changed;
            lock (_gate) { changed = _enabled != value; _enabled = value; }
            if (changed && !value) OnGameStopped();   // toggling off clears state immediately
        }
    }

    public event EventHandler? Changed;

    public void OnGameStarted(KnownGame game, int pid)
    {
        lock (_gate)
        {
            if (!_enabled) return;
            _watchingGame = game.DisplayName;
            _pid = pid;
            _spec = null;
            _latestSnapshot = null;
            _cpuWindow.Clear();
            _currently = Array.Empty<Problem>();
        }
        Changed?.Invoke(this, EventArgs.Empty);

        if (!string.IsNullOrWhiteSpace(game.SteamAppId))
            _ = FetchSpecAsync(game.SteamAppId);
    }

    public void OnGameStopped()
    {
        bool fire;
        lock (_gate)
        {
            fire = _watchingGame is not null || _currently.Count > 0;
            _watchingGame = null;
            _pid = 0;
            _spec = null;
            _latestSnapshot = null;
            _cpuWindow.Clear();
            _currently = Array.Empty<Problem>();
        }
        if (fire) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Test entry point — sample once and evaluate. Production code uses a timer over this.</summary>
    public async Task TickOnceAsync(CancellationToken ct = default)
    {
        int pid;
        SteamPcRequirements spec;
        lock (_gate)
        {
            if (_watchingGame is null) return;
            pid = _pid;
            spec = _spec ?? new SteamPcRequirements(null, null, null, null);
        }

        MetricsSnapshot snap;
        try { snap = await _sampler.SampleAsync(pid, ct); }
        catch { return; }

        IReadOnlyList<Problem> newSet;
        lock (_gate)
        {
            _latestSnapshot = snap;
            TrimAndPushCpu(snap);
            newSet = DetectionRules.Evaluate(snap, spec, _cpuWindow);
            var same = SameProblems(_currently, newSet);
            _currently = newSet;
            // Even when the problem set is unchanged, the latest snapshot is fresh — so
            // we always raise Changed; the VM reads LatestSnapshot to refresh the metric rows.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void TrimAndPushCpu(MetricsSnapshot snap)
    {
        // Push first so the test can see "one sample at At" even when window is empty.
        _cpuWindow.Enqueue((snap.At, snap.SystemCpuPercent));
        var cutoff = snap.At - CpuWindowSize;
        while (_cpuWindow.Count > 0 && _cpuWindow.Peek().At < cutoff)
            _cpuWindow.Dequeue();
    }

    private async Task FetchSpecAsync(string steamAppId)
    {
        try
        {
            var spec = await _specs.FetchAsync(steamAppId);
            if (spec is null) return;
            lock (_gate) _spec = spec;
        }
        catch { /* failure to fetch = silent degrade */ }
    }

    private static bool SameProblems(IReadOnlyList<Problem> a, IReadOnlyList<Problem> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Kind != b[i].Kind) return false;
        return true;
    }
}
```

- [ ] **Step 5.5: Run the tests to verify they pass**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter SentinelServiceTests --nologo -v minimal
```

Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 5.6: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/ISentinelService.cs `
        src/PrimeOSTuner.Core/Sentinel/SentinelService.cs `
        src/PrimeOSTuner.Tests/Sentinel/SentinelServiceTests.cs

git commit -m "feat(sentinel): orchestrator with rolling-CPU window + Changed event"
```

---

## Task 6: AppSettings.SentinelEnabled

One-field change so settings can persist the master toggle.

**Files:**
- Modify: `src/PrimeOSTuner.Core/Settings/AppSettings.cs`

- [ ] **Step 6.1: Add the property**

Edit `src/PrimeOSTuner.Core/Settings/AppSettings.cs` — add **at the bottom of the existing `AppSettings` class, just before the closing brace**:

```csharp
    // Sentinel
    public bool SentinelEnabled { get; set; } = true;
```

- [ ] **Step 6.2: Verify build**

Run:

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6.3: Commit**

```powershell
git add src/PrimeOSTuner.Core/Settings/AppSettings.cs
git commit -m "feat(settings): SentinelEnabled (defaults on)"
```

---

## Task 7: Wire SentinelService into ProfileLifecycleService

Hook game-start / game-stop. Use the same optional-ctor-arg pattern as the existing `IBackgroundSuspenderService` (commit ed626de) so tests keep their existing constructor calls. The lifecycle layer also needs the running game's PID — `IGameProcessWatcher` already passes a `KnownGame` on `GameStarted`; the pid is not currently in `GameStoppedArgs` either, so we look it up from `Process.GetProcessesByName` when starting.

**Files:**
- Modify: `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`

- [ ] **Step 7.1: Add the dependency + wiring**

In `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`:

Add `using PrimeOSTuner.Core.Sentinel;` to the imports.

Replace the constructor + fields:

```csharp
    private readonly IGameProcessWatcher _watcher;
    private readonly GameProfileStore _profiles;
    private readonly ActiveTweaksStore _active;
    private readonly IReadOnlyDictionary<string, ModeProfile> _profileLookup;
    private readonly ProfileApplier _applier;
    private readonly IBackgroundSuspenderService? _suspender;
    private readonly ISentinelService? _sentinel;

    public ProfileLifecycleService(
        IGameProcessWatcher watcher,
        GameProfileStore profiles,
        ActiveTweaksStore active,
        IReadOnlyDictionary<string, ModeProfile> profileLookup,
        ProfileApplier applier,
        IBackgroundSuspenderService? suspender = null,
        ISentinelService? sentinel = null)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
        _suspender = suspender;
        _sentinel = sentinel;
    }
```

Inside `OnGameStarted`, **after** the suspender block, add:

```csharp
            try
            {
                if (_sentinel is not null)
                {
                    var pid = System.Diagnostics.Process.GetProcessesByName(
                                  game.ExecutableNames[0].Replace(".exe", "", StringComparison.OrdinalIgnoreCase))
                              .Select(p => p.Id)
                              .FirstOrDefault();
                    _sentinel.OnGameStarted(game, pid);
                }
            }
            catch { /* Sentinel must never break a game launch */ }
```

Inside `OnGameStopped`, **before** the existing suspender resume block, add:

```csharp
            try { _sentinel?.OnGameStopped(); }
            catch { /* never trap on Sentinel teardown */ }
```

- [ ] **Step 7.2: Verify the project builds**

Run:

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 7.3: Run the full test suite to verify nothing else broke**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --nologo -v minimal
```

Expected: all tests pass (count includes the new Sentinel tests).

- [ ] **Step 7.4: Commit**

```powershell
git add src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs
git commit -m "feat(lifecycle): wire Sentinel into game launch/exit"
```

---

## Task 8: SentinelViewModel

Marshals `SentinelService.Changed` onto the WPF dispatcher and exposes bindable properties. Includes a separate `SentinelRowViewModel` per metric row.

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/SentinelRowViewModel.cs`
- Create: `src/PrimeOSTuner.UI/ViewModels/SentinelViewModel.cs`

- [ ] **Step 8.1: Create the row VM**

`src/PrimeOSTuner.UI/ViewModels/SentinelRowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SentinelRowViewModel : ObservableObject
{
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _current = "—";
    [ObservableProperty] private string _recommended = "—";
    [ObservableProperty] private bool _isProblem;
    [ObservableProperty] private string _explanation = "";
}
```

- [ ] **Step 8.2: Create the main VM**

`src/PrimeOSTuner.UI/ViewModels/SentinelViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Sentinel;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SentinelViewModel : ObservableObject
{
    private readonly ISentinelService _service;
    private bool _redDotLatched;   // stays true until the user opens the tab

    public SentinelRowViewModel Vram { get; } = new() { Label = "VRAM" };
    public SentinelRowViewModel Ram  { get; } = new() { Label = "System RAM" };
    public SentinelRowViewModel Cpu  { get; } = new() { Label = "System CPU" };

    public ObservableCollection<Problem> RecentAlerts { get; } = new();

    [ObservableProperty] private string _status = "Not watching any game right now.";
    [ObservableProperty] private bool _hasActiveProblem;

    public SentinelViewModel(ISentinelService service)
    {
        _service = service;
        _service.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    public void AcknowledgeDot() { _redDotLatched = false; HasActiveProblem = false; }

    private void Refresh()
    {
        Status = _service.WatchingGame is null
            ? "Not watching any game right now."
            : $"Watching: {_service.WatchingGame}";

        Reset(Vram); Reset(Ram); Reset(Cpu);
        PopulateLiveValues(_service.LatestSnapshot, _service.CurrentSpec);
        foreach (var p in _service.Currently) ApplyProblem(p);

        if (_service.Currently.Count > 0)
        {
            _redDotLatched = true;
            foreach (var p in _service.Currently)
                if (!RecentAlerts.Any(a => a.Kind == p.Kind && a.DetectedAt == p.DetectedAt))
                    RecentAlerts.Insert(0, p);
            while (RecentAlerts.Count > 5) RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }
        HasActiveProblem = _redDotLatched;
    }

    private void PopulateLiveValues(MetricsSnapshot? snap, SteamPcRequirements? spec)
    {
        if (snap is not null)
        {
            Vram.Current = snap.VramUsedBytes < 0 || snap.VramTotalBytes <= 0
                ? "VRAM unavailable on this system"
                : $"{snap.VramUsedBytes / 1024 / 1024} MB of {snap.VramTotalBytes / 1024 / 1024} MB";
            Ram.Current  = snap.RamUsedBytes  < 0 || snap.RamTotalBytes  <= 0
                ? "RAM unavailable"
                : $"{snap.RamUsedBytes  / 1024 / 1024} MB of {snap.RamTotalBytes  / 1024 / 1024} MB";
            Cpu.Current  = snap.SystemCpuPercent < 0
                ? "CPU unavailable"
                : $"{snap.SystemCpuPercent:F0}% (system)";
        }

        Vram.Recommended = spec?.RecVramMb is int v ? $"Game wants ≥ {v} MB" : "(spec unknown)";
        Ram.Recommended  = spec?.RecRamMb  is int r ? $"Game wants ≥ {r} MB" : "(spec unknown)";
        Cpu.Recommended  = "Watching for sustained 90%+";
    }

    private static void Reset(SentinelRowViewModel row)
    {
        row.IsProblem = false;
        row.Explanation = "";
    }

    private void ApplyProblem(Problem p)
    {
        var row = p.Kind switch
        {
            ProblemKind.VramOverhead => Vram,
            ProblemKind.RamPressure  => Ram,
            ProblemKind.CpuSaturated => Cpu,
            _ => null
        };
        if (row is null) return;
        row.IsProblem = true;
        row.Explanation = p.Detail;
    }
}
```

- [ ] **Step 8.3: Verify build**

Run:

```powershell
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
```

Expected: `Build succeeded.`.

- [ ] **Step 8.4: Commit**

```powershell
git add src/PrimeOSTuner.UI/ViewModels/SentinelRowViewModel.cs `
        src/PrimeOSTuner.UI/ViewModels/SentinelViewModel.cs
git commit -m "feat(sentinel): WPF view-model wired to ISentinelService"
```

---

## Task 9: SentinelView (XAML + code-behind) + IconSentinel + nav tab + red-dot indicator

**Files:**
- Create: `src/PrimeOSTuner.UI/Views/SentinelView.xaml`
- Create: `src/PrimeOSTuner.UI/Views/SentinelView.xaml.cs`
- Modify: `src/PrimeOSTuner.UI/Theme/Icons.xaml`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`

- [ ] **Step 9.1: Add IconSentinel**

In `src/PrimeOSTuner.UI/Theme/Icons.xaml`, just **before the closing `</ResourceDictionary>` tag**, add:

```xml
    <!-- SENTINEL — eye (Lucide) -->
    <DrawingImage x:Key="IconSentinel">
        <DrawingImage.Drawing>
            <GeometryDrawing>
                <GeometryDrawing.Pen>
                    <Pen Brush="{StaticResource IconBrush}" Thickness="2" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
                </GeometryDrawing.Pen>
                <GeometryDrawing.Geometry>
                    <PathGeometry Figures="M 2,12 s 3.5,-7 10,-7 s 10,7 10,7 s -3.5,7 -10,7 s -10,-7 -10,-7 z M 12,15 a 3,3 0 1 0 0,-6 a 3,3 0 0 0 0,6"/>
                </GeometryDrawing.Geometry>
            </GeometryDrawing>
        </DrawingImage.Drawing>
    </DrawingImage>
```

- [ ] **Step 9.2: Create SentinelView.xaml**

`src/PrimeOSTuner.UI/Views/SentinelView.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Views.SentinelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <UserControl.Resources>
        <Style x:Key="MetricRowCard" TargetType="Border" BasedOn="{StaticResource CardBorder}">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsProblem}" Value="True">
                    <Setter Property="BorderBrush" Value="#FF6A6A"/>
                    <Setter Property="BorderThickness" Value="2"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Padding="0,0,16,0">
        <StackPanel Margin="0,0,0,32">
            <TextBlock Text="Sentinel" Style="{StaticResource HeaderText}" Margin="0,0,0,8"/>
            <TextBlock Text="{Binding Status}" Style="{StaticResource SubHeaderText}" Margin="0,0,0,20"/>

            <TextBlock Text="LIVE METRICS" Style="{StaticResource SectionLabel}"/>

            <Border Style="{StaticResource MetricRowCard}" DataContext="{Binding Vram}" Margin="0,0,0,8">
                <StackPanel>
                    <TextBlock Text="{Binding Label}" FontWeight="SemiBold" FontSize="14"/>
                    <TextBlock Text="{Binding Current}" Foreground="{StaticResource Text2Brush}" FontSize="12"/>
                    <TextBlock Text="{Binding Recommended}" Foreground="{StaticResource Text3Brush}" FontSize="11" Margin="0,2,0,0"/>
                    <TextBlock Text="{Binding Explanation}" Foreground="#FFB0B0" FontSize="11" Margin="0,6,0,0"
                               TextWrapping="Wrap"
                               Visibility="{Binding IsProblem, Converter={StaticResource BoolToVisibility}}"/>
                </StackPanel>
            </Border>

            <Border Style="{StaticResource MetricRowCard}" DataContext="{Binding Ram}" Margin="0,0,0,8">
                <StackPanel>
                    <TextBlock Text="{Binding Label}" FontWeight="SemiBold" FontSize="14"/>
                    <TextBlock Text="{Binding Current}" Foreground="{StaticResource Text2Brush}" FontSize="12"/>
                    <TextBlock Text="{Binding Recommended}" Foreground="{StaticResource Text3Brush}" FontSize="11" Margin="0,2,0,0"/>
                    <TextBlock Text="{Binding Explanation}" Foreground="#FFB0B0" FontSize="11" Margin="0,6,0,0"
                               TextWrapping="Wrap"
                               Visibility="{Binding IsProblem, Converter={StaticResource BoolToVisibility}}"/>
                </StackPanel>
            </Border>

            <Border Style="{StaticResource MetricRowCard}" DataContext="{Binding Cpu}" Margin="0,0,0,18">
                <StackPanel>
                    <TextBlock Text="{Binding Label}" FontWeight="SemiBold" FontSize="14"/>
                    <TextBlock Text="{Binding Current}" Foreground="{StaticResource Text2Brush}" FontSize="12"/>
                    <TextBlock Text="{Binding Recommended}" Foreground="{StaticResource Text3Brush}" FontSize="11" Margin="0,2,0,0"/>
                    <TextBlock Text="{Binding Explanation}" Foreground="#FFB0B0" FontSize="11" Margin="0,6,0,0"
                               TextWrapping="Wrap"
                               Visibility="{Binding IsProblem, Converter={StaticResource BoolToVisibility}}"/>
                </StackPanel>
            </Border>

            <TextBlock Text="RECENT ALERTS (THIS SESSION)" Style="{StaticResource SectionLabel}"/>
            <Border Style="{StaticResource CardBorder}" Margin="0,0,0,18">
                <ItemsControl ItemsSource="{Binding RecentAlerts}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Margin="0,4">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0"
                                           Text="{Binding DetectedAt, StringFormat='{}{0:HH:mm:ss}'}"
                                           Foreground="{StaticResource Text3Brush}"
                                           FontSize="11" Margin="0,0,10,0"/>
                                <TextBlock Grid.Column="1"
                                           Text="{Binding Detail}"
                                           Foreground="{StaticResource Text0Brush}"
                                           FontSize="12" TextWrapping="Wrap"/>
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Border>

            <TextBlock TextWrapping="Wrap" Foreground="{StaticResource Text3Brush}" FontSize="11">
                Sentinel watches passively. No notifications, no popups. A red dot on the tab is the only signal — open the tab to see what's wrong.
            </TextBlock>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 9.3: Create SentinelView.xaml.cs**

`src/PrimeOSTuner.UI/Views/SentinelView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class SentinelView : UserControl
{
    private readonly SentinelViewModel _vm;

    public SentinelView(SentinelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => _vm.AcknowledgeDot();
    }
}
```

- [ ] **Step 9.4: Add the Sentinel nav button + red-dot overlay to MainWindow.xaml**

In `src/PrimeOSTuner.UI/MainWindow.xaml`, find the existing `NavSettings` button and **insert immediately before it**:

```xml
                    <Button x:Name="NavSentinel" Tag="Sentinel" Click="NavButton_Click" Style="{StaticResource TopTab}">
                        <Grid>
                            <StackPanel Orientation="Horizontal">
                                <Image Source="{StaticResource IconSentinel}" Width="16" Height="16" Margin="0,0,6,0"/>
                                <TextBlock Text="Sentinel" VerticalAlignment="Center"/>
                            </StackPanel>
                            <Ellipse x:Name="SentinelDot" Width="6" Height="6" Fill="#FF6A6A"
                                     HorizontalAlignment="Right" VerticalAlignment="Top"
                                     Margin="0,4,-2,0" Visibility="Collapsed">
                                <Ellipse.Effect>
                                    <DropShadowEffect Color="#FF6A6A" ShadowDepth="0" BlurRadius="6" Opacity="0.85"/>
                                </Ellipse.Effect>
                            </Ellipse>
                        </Grid>
                    </Button>
```

- [ ] **Step 9.5: Wire Sentinel into the tab dictionary + dot binding**

In `src/PrimeOSTuner.UI/MainWindow.xaml.cs`:

Add `using PrimeOSTuner.Core.Sentinel;` to imports.

Add a constructor parameter so MainWindow learns about the VM (DI will provide it):

```csharp
    private readonly ShellViewModel _shellVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly SentinelViewModel _sentinelVm;
    private Dictionary<string, Button>? _tabs;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm,
                      SettingsViewModel settingsVm, SentinelViewModel sentinelVm)
    {
        InitializeComponent();
        _shellVm = vm;
        _settingsVm = settingsVm;
        _sentinelVm = sentinelVm;
        DataContext = vm;
```

In the same constructor, append after the existing dictionary entries:

```csharp
            ["Sentinel"]    = NavSentinel,
```

(Keeping the trailing comma + closing brace.)

Then before `ShowTab("Dashboard");` add:

```csharp
        _sentinelVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SentinelViewModel.HasActiveProblem))
                SentinelDot.Visibility = _sentinelVm.HasActiveProblem
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        };
        SentinelDot.Visibility = _sentinelVm.HasActiveProblem
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
```

In `ShowTab`, add a case before the default:

```csharp
            "Sentinel"     => sp.GetRequiredService<Views.SentinelView>(),
```

- [ ] **Step 9.6: Verify build**

Run:

```powershell
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
```

Expected: `Build succeeded.`. (DI is not yet wired in App.xaml.cs — runtime will fail until Task 10. Build alone must succeed.)

- [ ] **Step 9.7: Commit**

```powershell
git add src/PrimeOSTuner.UI/Theme/Icons.xaml `
        src/PrimeOSTuner.UI/Views/SentinelView.xaml `
        src/PrimeOSTuner.UI/Views/SentinelView.xaml.cs `
        src/PrimeOSTuner.UI/MainWindow.xaml `
        src/PrimeOSTuner.UI/MainWindow.xaml.cs

git commit -m "feat(sentinel): tab UI + nav button + red-dot indicator"
```

---

## Task 10: DI registration in App.xaml.cs

Wires every new service into the host container and updates the existing `ProfileLifecycleService` factory to pass the Sentinel service in.

**Files:**
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 10.1: Add the using directives**

At the top of `src/PrimeOSTuner.UI/App.xaml.cs`, add:

```csharp
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Win.Sentinel;
```

- [ ] **Step 10.2: Register the services**

Right **after** the existing `// Background suspender …` block and **before** the `// Lifecycle` comment, insert:

```csharp
                // Sentinel — passive performance watcher
                s.AddSingleton<IMetricsSampler>(_ => new GpuPerfCounterMetricsSampler());
                s.AddHttpClient<ISpecFetcher, SteamSpecFetcher>(c =>
                {
                    c.BaseAddress = new Uri("https://store.steampowered.com");
                    c.Timeout = TimeSpan.FromSeconds(15);
                });
                s.AddSingleton<ISentinelService>(sp =>
                    new SentinelService(
                        sp.GetRequiredService<ISpecFetcher>(),
                        sp.GetRequiredService<IMetricsSampler>()));
                s.AddSingleton<SentinelViewModel>();
                s.AddTransient<Views.SentinelView>();
```

- [ ] **Step 10.3: Pass Sentinel into the ProfileLifecycleService factory**

Find the existing `ProfileLifecycleService` factory and replace the `return new ProfileLifecycleService(...)` call's argument list with:

```csharp
                    return new ProfileLifecycleService(
                        sp.GetRequiredService<IGameProcessWatcher>(),
                        sp.GetRequiredService<GameProfileStore>(),
                        sp.GetRequiredService<ActiveTweaksStore>(),
                        dict,
                        sp.GetRequiredService<ProfileApplier>(),
                        sp.GetRequiredService<IBackgroundSuspenderService>(),
                        sp.GetRequiredService<ISentinelService>());
```

- [ ] **Step 10.4: Sync the persisted setting into the live ISentinelService at startup**

Right after the `var settings = Host.Services.GetRequiredService<SettingsViewModel>();` line (in `OnStartup`), add:

```csharp
        var sentinelSvc = Host.Services.GetRequiredService<ISentinelService>();
        sentinelSvc.Enabled = settings.SentinelEnabled;
```

This makes sure the loaded-from-disk setting is applied to the live service before any game lifecycle event can fire. The `SettingsViewModel.SentinelEnabled` change handler (Step 10.5) propagates further changes at runtime.

- [ ] **Step 10.5: Add the SettingsViewModel.SentinelEnabled property that propagates into the service**

In `src/PrimeOSTuner.UI/ViewModels/SettingsViewModel.cs`, mirror the new field on the VM. Pattern follows existing settings: locate the `NotificationsEnabled` property (or the closest analogue) and add:

```csharp
    [ObservableProperty] private bool _sentinelEnabled;

    partial void OnSentinelEnabledChanged(bool value)
    {
        _settings.SentinelEnabled = value;
        _store.Save(_settings);
        _sentinel.Enabled = value;
    }
```

The handler propagates the change to `ISentinelService` so toggling at runtime clears any in-flight watcher (`Enabled = false` calls `OnGameStopped` internally).

`SettingsViewModel` needs an `ISentinelService` constructor parameter. Add it alongside the existing dependencies and assign to a private `_sentinel` field. If the VM doesn't already have `_settings` / `_store` fields, mirror the existing constructor that backs `NotificationsEnabled`. **Read SettingsViewModel before editing — names may differ slightly.**

Initialize `_sentinelEnabled` from `_settings.SentinelEnabled` in the constructor where the other fields are initialized.

- [ ] **Step 10.6: Add a Sentinel toggle row to SettingsView.xaml**

Open `src/PrimeOSTuner.UI/Views/SettingsView.xaml`. Find the existing "Notifications" toggle row (or any similar `ToggleSwitchStyle` row), and add a sibling row above or below:

```xml
                <Grid Margin="0,8,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0">
                        <TextBlock Text="Sentinel (performance watcher)"
                                   FontSize="13" FontWeight="SemiBold"/>
                        <TextBlock Text="Compares the running game's live resource use to its Steam spec. Surfaces a subtle red dot on the Sentinel tab when something looks wrong."
                                   Foreground="{StaticResource Text3Brush}" FontSize="11"
                                   TextWrapping="Wrap" Margin="0,2,0,0"/>
                    </StackPanel>
                    <ToggleButton Grid.Column="1"
                                  Style="{StaticResource ToggleSwitchStyle}"
                                  IsChecked="{Binding SentinelEnabled, Mode=TwoWay}"
                                  VerticalAlignment="Center"/>
                </Grid>
```

- [ ] **Step 10.7: Build + run the test suite**

Run:

```powershell
dotnet build PrimeOSTuner.sln --nologo -v minimal
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --nologo -v minimal
```

Expected: build succeeds; all tests pass.

- [ ] **Step 10.8: Commit**

```powershell
git add src/PrimeOSTuner.UI/App.xaml.cs `
        src/PrimeOSTuner.UI/ViewModels/SettingsViewModel.cs `
        src/PrimeOSTuner.UI/Views/SettingsView.xaml
git commit -m "feat(sentinel): DI registration + Settings toggle"
```

---

## Task 11: Production sample loop — start a Timer in SentinelService

Tests use `TickOnceAsync`; production needs a real timer. We bolt it on as a small additional API so the test-only entry point stays untouched.

**Files:**
- Modify: `src/PrimeOSTuner.Core/Sentinel/SentinelService.cs`

- [ ] **Step 11.1: Add a Timer-driven sample loop**

In `SentinelService.cs`, add a `using System.Threading;` directive if not already present.

Add new field:

```csharp
    private System.Threading.Timer? _timer;
    private static readonly TimeSpan SamplePeriod = TimeSpan.FromSeconds(4);
```

In `OnGameStarted`, **after** the `Changed?.Invoke(...)` line, add:

```csharp
        _timer ??= new System.Threading.Timer(_ => _ = TickOnceAsync(), null, SamplePeriod, SamplePeriod);
```

In `OnGameStopped`, **before** the `if (fire) Changed?.Invoke(...)` line, add:

```csharp
        _timer?.Dispose();
        _timer = null;
```

- [ ] **Step 11.2: Verify tests still pass**

Run:

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --nologo -v minimal
```

Expected: all green. (Tests drive `TickOnceAsync` directly — they never start the timer.)

- [ ] **Step 11.3: Commit**

```powershell
git add src/PrimeOSTuner.Core/Sentinel/SentinelService.cs
git commit -m "feat(sentinel): 4-second production sample timer"
```

---

## Task 12: Manual verification + final commit-free smoke

No tests can prove the UI feels right or that the dot actually appears at the right time. Run the app and check.

**Files:** none modified.

- [ ] **Step 12.1: Build + launch**

```powershell
Stop-Process -Name "PrimeOSTuner.UI" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
Start-Process "src\PrimeOSTuner.UI\bin\Debug\net9.0-windows\PrimeOSTuner.UI.exe"
```

- [ ] **Step 12.2: Verify the Sentinel tab is present**

Look in the top nav strip — there should be a "Sentinel" button immediately before "Settings", with the eye icon, and no red dot when no game is running.

- [ ] **Step 12.3: Verify the empty-state**

Click the Sentinel tab. Expected display:
- Header: "Sentinel"
- Sub-header: "Not watching any game right now."
- Three cards: VRAM / System RAM / System CPU, all neutral (no red border).
- Empty Recent Alerts card.
- Tip text at the bottom about no notifications.

- [ ] **Step 12.4: Verify the Settings toggle**

Click "Settings". Find the Sentinel row. Toggle it off and on — UI should respond instantly with no errors.

- [ ] **Step 12.5: Verify the dot appears with a simulated problem**

In the file `src/PrimeOSTuner.UI/App.xaml.cs`, add a temporary debug call right after `tray.ShowRequested += ...`:

```csharp
        // TEMP — for manual verification of Task 12. Remove before final commit.
        var sentinel = Host.Services.GetRequiredService<ISentinelService>();
        sentinel.OnGameStarted(new PrimeOSTuner.Core.Games.KnownGame(
            "test", "Eagle Eye Test", new[] { "explorer.exe" }, null, null,
            PrimeOSTuner.Core.Games.KnownGameSource.UserAdded), 0);
```

Run the app again, wait ~10 seconds, confirm the red dot appears on the Sentinel tab when the CPU rule (or VRAM rule) fires on your system. Open the tab — the dot should clear immediately.

**Then revert the temporary edit before the final commit.**

- [ ] **Step 12.6: Verify the run with a real game**

Launch any Library game that has a Steam app id. Within ~10–20 seconds the Sentinel tab's status should update to "Watching: <game>". If your system has plenty of headroom for that game, the dot stays hidden — that's correct behaviour. Force a problem (open dozens of browser tabs etc.) and confirm the dot appears.

- [ ] **Step 12.7: Final cleanup commit (if anything was changed for verification)**

If Task 12 left any verification scaffolding in source, remove it now and commit:

```powershell
git diff           # confirm only the verification scaffolding is being reverted
git add -p         # interactively stage the reversion
git commit -m "chore(sentinel): remove manual-verification scaffolding"
```

---

## Spec coverage check

| Spec requirement | Covered by |
| --- | --- |
| Name "Sentinel", master toggle, dot clears on open, alerts session-only | Tasks 6, 8, 9, 10 |
| VRAM / RAM / CPU detection axes with "silent on uncertainty" | Task 2 |
| Steam HTML parser with three fixture styles | Task 1 |
| Disk-cached Steam fetch | Task 3 |
| Win11 GPU/CPU/RAM perf-counter sampler | Task 4 |
| 4-second sampling cadence | Tasks 5, 11 |
| Rolling 30 s CPU window | Tasks 2, 5 |
| Wiring through `ProfileLifecycleService.GameStarted/Stopped` | Task 7 |
| Subtle red dot on Sentinel nav-tab icon, color `#FF6A6A` | Task 9 |
| Three per-axis rows, recent-alerts card, status header | Task 9 |
| Settings tab toggle defaulting ON | Tasks 6, 10 |
| Tests: parser fixtures, detection rules, service orchestration | Tasks 1, 2, 5 |
| DI registration | Task 10 |
| Out-of-scope (thermals, FPS, desktop-idle rules) — none of these built | — |
