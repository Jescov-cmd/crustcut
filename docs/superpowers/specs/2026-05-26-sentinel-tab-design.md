# Sentinel — passive performance watcher

**Status:** approved — ready for implementation plan
**Date:** 2026-05-26
**Author:** Claude (working under PrimeOS Tuner v0.4 honest-optimization rules)

## Goal

Add a new tab that **passively watches** the currently-running game's resource
usage and compares it against the game's Steam-listed minimum / recommended
specs. If something looks wrong — too much VRAM in use for a game that doesn't
need it, system RAM near full, sustained CPU pegged — show a **subtle red dot
on the tab's nav-bar icon**. No notifications. No popups. No audio.

The dot clears when the user opens the tab. Inside the tab, a row per metric
shows current vs. recommended and (if red) a one-sentence explanation.

## Name

Recommended: **Sentinel**.
- Reads as "passive watcher, fires only on a tripwire"
- Not used by any other PC optimizer I'm aware of (uniqueness constraint)
- Single word, professional, fits the project's restyle direction

Alternative the user suggested: **Eagle Eye** — fine, but slightly more
generic. Final choice deferred to user on spec review.

## Non-goals (v1)

- **Frame-time profiling / FPS drop detection.** That belongs to the
  PresentMon-based Phase 2 item, separate scope.
- **Thermal / power monitoring.** Needs `LibreHardwareMonitor` or vendor
  APIs; defer to Sentinel v2.
- **Recommendations or auto-fixes.** Sentinel only *diagnoses*. Acting on
  findings is the user's call.
- **Per-process attribution beyond the game.** "Some background app is
  hogging VRAM" is the verdict; identifying *which* app is a v2 concern.
- **Offline mode for spec data.** Steam API failure means "we don't know" —
  we silently degrade, never fabricate a spec.

## Scope (v1, medium)

Three detection axes, all sharing the same Steam-spec data source so adding
them all at once is roughly the same effort as VRAM alone:

| Axis | "Problem" condition |
| --- | --- |
| VRAM | dedicated VRAM usage > 95 % of card capacity AND game's recommended VRAM ≤ 50 % of card capacity |
| RAM  | system RAM usage > 95 % AND game's recommended RAM ≤ 75 % of total RAM |
| CPU  | system CPU > 90 % across **every sample in the trailing 30 s window** (≈ 8 consecutive samples at the 4 s cadence) while a Library game is the foreground process |

If a Steam-spec field is missing or unparseable, that axis stays *silent*
for that game (no false positives from bad data).

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  MainWindow nav-tab strip                                │
│  ┌──────┐ ┌──────┐ ... ┌──────────────┐                  │
│  │ Dash │ │ Opt  │ ... │ Sentinel  ●  │  ← red dot       │
│  └──────┘ └──────┘     └──────────────┘                  │
│                              ▲                            │
│  (indicator bound to SentinelViewModel.HasActiveProblem) │
└──────────────────────────────────────────────────────────┘
                               │
                  ┌────────────┴────────────┐
                  │   SentinelViewModel     │
                  └────────────┬────────────┘
                               │
                  ┌────────────┴────────────┐
                  │   SentinelService       │ ← orchestrates everything,
                  │   (singleton)           │   raises Changed event
                  └─┬──────────────────┬────┘
                    │                  │
        ┌───────────┴─────┐   ┌────────┴──────────┐
        │  SpecFetcher    │   │  MetricsSampler   │
        │  (HTTP + parse) │   │  (perf counters)  │
        └─────────────────┘   └───────────────────┘
                ▲                       ▲
                │                       │
         Steam Store API         Win11 perf counters
         (cached locally)         + P/Invoke GlobalMemoryStatusEx
```

### Game-state hookup

Sentinel reuses the existing `ProfileLifecycleService.GameStarted /
GameStopped` events (already proven by the background suspender wiring,
commit ed626de). When a Library game starts:

1. `SentinelService.OnGameStarted(KnownGame)` is called
2. If we have a cached parsed spec for that Steam appid, use it
3. Else fire-and-forget `SpecFetcher.FetchAsync(appid)` — failures swallowed
4. Start the 4-second sample loop
5. On each sample, evaluate detection rules; raise `Changed` if state flips

When the game stops, the loop stops; the indicator goes back to neutral.

## Components

### `PrimeOSTuner.Core.Sentinel`

| Type | Responsibility |
| --- | --- |
| `SteamPcRequirements` | Parsed `record` with `int? MinRamMb`, `int? RecRamMb`, `int? MinVramMb`, `int? RecVramMb`, `string? MinCpuModel`, `string? RecCpuModel`. Nullable fields = parser couldn't find that value. |
| `ISpecFetcher` | `Task<SteamPcRequirements?> FetchAsync(int appid, CancellationToken)`. Returns null on any failure. |
| `SteamSpecFetcher` | Concrete impl. Calls `https://store.steampowered.com/api/appdetails?appids={appid}&filters=basic`, deserializes JSON, parses HTML in `pc_requirements.minimum/recommended`. Cached on disk under `%LocalAppData%\PrimeOSTuner\sentinel-specs.json`. |
| `SteamSpecParser` (static) | Pure parser. Input: spec HTML string. Output: extracted ints. Tested with fixture payloads. |
| `Problem` (record) | `ProblemKind Kind`, `string Detail`, `DateTime DetectedAt`. |
| `ProblemKind` (enum) | `VramOverhead`, `RamPressure`, `CpuSaturated`. |
| `DetectionRules` (static) | Pure functions: given a `MetricsSnapshot` + parsed spec + system capacities, return `IReadOnlyList<Problem>`. |
| `MetricsSnapshot` (record) | `double CpuPercent`, `long RamUsedBytes`, `long RamTotalBytes`, `long VramUsedBytes`, `long VramTotalBytes`, `int GamePid`, `DateTime At`. |
| `IMetricsSampler` | `Task<MetricsSnapshot> SampleAsync(int gamePid, CancellationToken)`. |
| `SentinelService` | Orchestration. Holds last-known `IReadOnlyList<Problem>`, raises `Changed`, exposes `Currently`. Sustained-CPU rule needs a rolling 30 s window — keep a `Queue<(DateTime, double)>` of recent samples. |

### `PrimeOSTuner.Win.Sentinel`

| Type | Responsibility |
| --- | --- |
| `GpuPerfCounterMetricsSampler` | Concrete `IMetricsSampler`. Reads `\GPU Engine(*)\Utilization Percentage` (sum of `engtype_3D` instances), `\GPU Adapter Memory(*)\Dedicated Usage` summed across adapters, plus existing system CPU / RAM counters. Win11 only. |

### `PrimeOSTuner.UI`

| Type | Responsibility |
| --- | --- |
| `SentinelViewModel` | Subscribes to `SentinelService.Changed`. Exposes `bool HasActiveProblem`, `string GameName`, three per-axis row VMs (`VramRow`, `RamRow`, `CpuRow` each with `Label`, `Value`, `Recommended`, `IsProblem`), and a bounded `RecentAlerts` collection. |
| `SentinelView.xaml` | The tab. Header ("Watching: <game>" or "(no game running)"), three metric rows, recent-alerts list, master Enabled toggle. |
| Nav-tab indicator | Filled circle ≈ 6 px diameter overlaid on the top-right corner of `IconSentinel` in `MainWindow.xaml`. Fill `#FF6A6A` (same red as the gem-tier 3 card). Drop shadow same colour for legibility. `Visibility` bound to `SentinelViewModel.HasActiveProblem`. |
| `IconSentinel` | New Lucide-style icon in `Icons.xaml`. Recommended: eye outline (clean, on-name). |

## Settings

One new field on `AppSettingsStore`:

```csharp
public bool SentinelEnabled { get; set; } = true;
```

Toggle exposed both inside the Sentinel tab and (mirror) on Settings.

## Data flow walkthrough

User launches Cyberpunk 2077 (already in Library):

1. `GameProcessWatcher` detects the process → `ProfileLifecycleService.GameStarted` event fires
2. `ProfileLifecycleService` calls (a) the existing tweak applier, (b) the background suspender, AND (new) (c) `SentinelService.OnGameStarted(game)`
3. `SentinelService` looks up cached spec for Cyberpunk's appid (1091500). Cache miss → spawns background `SteamSpecFetcher.FetchAsync(1091500)`
4. Sampling loop starts immediately (CPU/RAM rules don't need spec yet; VRAM rule will skip until spec arrives or stays silent forever if Steam is offline)
5. Spec arrives: `{ MinRamMb: 8192, RecRamMb: 12288, RecVramMb: 6144, ... }`
6. Each 4 s tick: `GpuPerfCounterMetricsSampler.SampleAsync(pid)` → `MetricsSnapshot`
7. `DetectionRules.Evaluate(snapshot, spec, capacities)` returns `[]` or `[Problem(VramOverhead, "Game's spec says 6 GB VRAM; system is using 11.5 GB of 12 GB")]`
8. If the problem set transitions from empty to non-empty (or vice versa), `Changed` fires
9. ViewModel updates `HasActiveProblem` → red dot appears on nav tab
10. User clicks tab → `HasActiveProblem` set to false (acknowledged), red dot clears; rows show details

When Cyberpunk exits:
- `GameStopped` → `SentinelService.OnGameStopped()` → sampling stops, `Currently = []`, `Changed` fires, indicator clears.

## Error handling

| Failure | Behavior |
| --- | --- |
| Steam API HTTP failure | Log warning, no spec, VRAM/RAM rules stay silent for that game until Steam comes back. CPU rule still works. |
| Steam HTML doesn't parse | Same as above — `null` fields, silent on those axes. |
| Win11 GPU counters not available (Win10) | `GpuPerfCounterMetricsSampler` returns `VramUsedBytes = -1`. VRAM rule treats negative as "unknown" and stays silent. |
| Permission denied on a counter | Same: -1 / null and stay silent. |
| The watched game's pid disappears mid-sample | Loop exits gracefully on next tick. |

Sentinel's prime directive: **silent on uncertainty.** Never a false red dot.

## Testing strategy

| Layer | How tested |
| --- | --- |
| `SteamSpecParser` | TDD with fixture HTML payloads in `tests/Fixtures/steam-specs/`. Cover the three common Steam template formats (Valve, Bethesda-style, indie minimal). |
| `DetectionRules` | Pure functions, parameterized xUnit tests with synthetic snapshots. |
| `SentinelService` | Mock `ISpecFetcher` + `IMetricsSampler`, virtual `DateTime` provider so 30 s window can be tested without sleeping. |
| `SentinelViewModel` | Verify it sets `HasActiveProblem` correctly on `Changed`, and clears it when the user opens the tab. |
| `GpuPerfCounterMetricsSampler` | Smoke-test that it returns positive numbers when run on a Win11 dev machine. Not unit-tested (real perf counters). |

Target: ~15 new passing tests; total should land at ~236.

## Files added

```
src/PrimeOSTuner.Core/Sentinel/
    SteamPcRequirements.cs
    ISpecFetcher.cs
    SteamSpecFetcher.cs
    SteamSpecParser.cs
    Problem.cs
    DetectionRules.cs
    MetricsSnapshot.cs
    IMetricsSampler.cs
    SentinelService.cs

src/PrimeOSTuner.Win/Sentinel/
    GpuPerfCounterMetricsSampler.cs

src/PrimeOSTuner.UI/
    ViewModels/SentinelViewModel.cs
    Views/SentinelView.xaml(.cs)
    (Icons.xaml gets IconSentinel)
    (MainWindow.xaml gets a Sentinel nav button + red-dot overlay)
    (App.xaml.cs gets DI registrations + SentinelEnabled wiring)

src/PrimeOSTuner.Tests/Sentinel/
    SteamSpecParserTests.cs
    DetectionRulesTests.cs
    SentinelServiceTests.cs

tests/Fixtures/steam-specs/
    valve-style.html
    bethesda-style.html
    indie-minimal.html
```

## Files modified

- `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs` — add optional `ISentinelService` ctor arg, call `OnGameStarted` / `OnGameStopped` (same pattern as the background suspender)
- `src/PrimeOSTuner.UI/App.xaml.cs` — DI registrations
- `src/PrimeOSTuner.UI/MainWindow.xaml` — new nav tab + indicator overlay
- `src/PrimeOSTuner.UI/MainWindow.xaml.cs` — case for "Sentinel" in tab dictionary
- `src/PrimeOSTuner.UI/Theme/Icons.xaml` — `IconSentinel`
- `src/PrimeOSTuner.Core/Settings/AppSettingsStore.cs` — `SentinelEnabled`

## Sampling cadence

4 seconds. Justification:
- Lower → cost overhead, especially perf counters on weaker CPUs
- Higher → 30 s sustained-CPU rule needs ~8 samples to fire, which is plenty

Cancellation: the loop respects a `CancellationTokenSource` owned by
`SentinelService`. When the service is disposed (app shutdown), the loop
exits cleanly.

## Decisions (resolved 2026-05-26)

1. **Name:** Sentinel.
2. **Master Enabled toggle:** defaults ON on first launch. Feature is
   invisible until something is wrong, so default-on is non-intrusive.
3. **Red dot:** clears when the user opens the Sentinel tab (treated as
   "acknowledged"), regardless of whether the underlying problem is still
   active. The in-tab rows continue to show the live state.
4. **Recent Alerts:** session-only — cleared on app restart. Long-term
   history is the History tab's job, not Sentinel's.

## Out of scope for v1, candidates for v2

- Thermal monitoring (LibreHardwareMonitor integration)
- Per-process VRAM attribution (which app is hogging it?)
- FPS / frame-time correlation (PresentMon)
- Sentinel rules for the desktop / non-game state — currently it sleeps
  when no game runs; we could later add "you're using 95 % of RAM on idle
  desktop" alerts
- User-configurable thresholds
