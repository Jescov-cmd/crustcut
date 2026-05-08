# PrimeOS Tuner — v1 Design Spec

**Status:** Approved (2026-05-07)
**Author:** Jaxson Lemperle (with AI pairing)
**Target ship:** v1 in 8–12 weeks at ~5–10 hrs/week

---

## 1. Context

The user wants to build a Windows PC performance optimizer modeled closely on **Hone** — the gaming-focused tuning tool known for its premium dark UI, one-click optimization, and gamer-targeted tweaks (input lag, FPS, debloat). The end goal is to surpass Hone, but v1 is scoped to a credible "Balanced" feature set rather than full feature parity.

This is the user's first real software project. Time budget is realistic (5–10 hrs/week with AI pairing), so the design favors:

- A buildable v1 over an exhaustive one
- Beginner-friendly tooling (free Visual Studio Community, mature WPF stack)
- Strong safety guardrails because the app modifies system state
- A polished aesthetic — the "Hone look" matters as much as the function

The intended outcome of v1: a polished, installable Windows app that genuinely improves PC performance, looks indistinguishable from a paid product, and is safe enough that a non-expert can use it without bricking their system.

---

## 2. Scope

### In scope (v1)

- 5-tab Hone-style desktop UI with live system monitoring
- 18 distinct optimization features (listed in §4)
- One-click optimization (safe tweaks only)
- Manual-select bloatware remover with per-app descriptions
- Game/FPS-focused tweak set (input lag, mouse, GPU prefs, network)
- System restore point integration before destructive actions
- Tweak history with one-click revert
- Installable Windows binary built with Velopack (code signing is *not* in v1 — it requires a paid certificate; Windows SmartScreen will warn on first install until reputation builds)
- Logging via Serilog for debugging and audit

### Out of scope (deferred to later versions)

- Per-game profiles (auto-detect VALORANT, apply gaming preset)
- Real-time in-game FPS overlay
- Driver detection / installation / update
- Custom theme / color scheme selection by the user
- Cloud telemetry, accounts, or licensing
- macOS / Linux support
- Multi-language localization
- CI/CD pipeline (manual builds for v1)

---

## 3. Tech Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 8 (LTS) |
| Language | C# 12 |
| UI framework | WPF |
| UI controls | WPF-UI (Fluent design) |
| MVVM helpers | CommunityToolkit.Mvvm |
| Live charts | LiveCharts2 |
| Hardware monitoring | LibreHardwareMonitorLib |
| Registry / WMI | Microsoft.Win32.Registry, System.Management |
| Logging | Serilog |
| Installer / updates | Velopack |
| Tests | xUnit |
| IDE | Visual Studio 2022 Community |

**Why this stack:** WPF + .NET 8 is the most mature, beginner-resourced way to build a polished Windows desktop app. WPF-UI lifts the visual quality close to "Hone-grade" without us hand-rolling controls. CommunityToolkit.Mvvm dramatically reduces MVVM boilerplate, which is the #1 friction point for beginner WPF developers.

---

## 4. Features (18 across 5 tabs)

### Dashboard tab — visualization only, no destructive actions

1. **Live system stats** — CPU, RAM, GPU, Disk, Network rendered as animated cards with sparkline graphs updating ~1 Hz
2. **Boost Score** — gamified 0–100 rating computed from current PC state (running services, telemetry status, applied tweaks, junk size)
3. **Quick action shortcuts** — large buttons jumping to One-Click Optimize, Game Boost, Bloatware

### Optimize tab — safe-by-default tweaks

4. **One-Click Optimize** — runs items 5–8 sequentially with progress UI; auto-creates a system restore point first
5. **Junk file cleaner** — temp dirs, browser cache, Windows update cache, DNS cache, recycle bin, with per-category checkboxes (everything checked by default — these are safe)
6. **Power plan optimizer** — switch to High Performance / Ultimate Performance plan
7. **RAM cleaner** — call EmptyWorkingSet on idle processes to free working-set RAM
8. **Visual effects optimizer** — disable animations, transparency, drop shadows for FPS

### Game Boost tab — latency / FPS focus

9. **Mouse acceleration toggle** — disables Windows pointer acceleration via registry (HKCU\Control Panel\Mouse)
10. **Mouse polling rate display** — shows current device polling rate (read-only in v1; cannot change without driver-level access)
11. **Timer resolution / low-latency mode** — sets Windows timer to 0.5 ms via NtSetTimerResolution P/Invoke
12. **Game Mode + GPU scheduling** — toggles AllowAutoGameMode and HwSchMode registry keys
13. **Per-app GPU preference** — sets installed games (auto-detected from Steam, Epic, Riot, Battle.net) to "High Performance" GPU
14. **TCP / network gaming tweaks** — disables Nagle's algorithm and tunes NetworkThrottlingIndex for known game install paths

### Bloatware tab — manual checklist, nothing checked by default

15. **Preinstalled app remover** — enumerates AppX packages (Candy Crush, Xbox, News, Weather, Cortana, etc.) with plain-language description per app; user checks what to remove and clicks "Apply selected"
16. **Telemetry disabler** — list of Windows telemetry services (DiagTrack, dmwappushservice, etc.) with descriptions; per-item checkbox

### System tab — safety net + transparency

17. **Restore point manager** — create restore point on demand; list existing points; restore to any point
18. **Tweak history / undo log** — chronological log of every change PrimeOS Tuner has made; per-entry "Revert" button where applicable

---

## 5. Architecture

Four projects in a single Visual Studio solution.

```
PrimeOSTuner.sln
├── PrimeOSTuner.UI/         (WPF executable — what the user sees)
├── PrimeOSTuner.Core/       (business logic, no Windows API directly)
├── PrimeOSTuner.Win/        (thin wrappers around Windows APIs)
└── PrimeOSTuner.Tests/      (xUnit unit tests)
```

### Layer 1 — `PrimeOSTuner.UI` (WPF)

- **Views/** — one XAML page per tab (DashboardView, OptimizeView, GameBoostView, BloatwareView, SystemView) plus `MainWindow.xaml` with sidebar navigation
- **ViewModels/** — one ViewModel per view, using `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm
- **Controls/** — reusable: `BoostScoreRing`, `StatCard`, `OptimizeButton`, `TweakChecklistItem`
- **Theme/** — color resources, control styles for the cyan-on-near-black Hone-like aesthetic
- **App.xaml.cs** — DI bootstrap (Microsoft.Extensions.Hosting), Serilog wiring, single-instance enforcement

### Layer 2 — `PrimeOSTuner.Core`

- **Tweaks/** — one class per tweak implementing a common `ITweak` interface:
  ```csharp
  public interface ITweak {
      string Id { get; }
      string DisplayName { get; }
      string Description { get; }
      bool RequiresElevation { get; }
      bool IsDestructive { get; }
      Task<TweakState> ProbeAsync();         // is it currently applied?
      Task<TweakResult> ApplyAsync();         // apply, return undo data
      Task<TweakResult> RevertAsync(string undoData);
      Task<string> PreviewAsync();           // human-readable diff
  }
  ```
- **Bloatware/** — `AppXScanner`, `TelemetryServiceCatalog` with per-item descriptions
- **Monitoring/** — `SystemSampler` (1 Hz CPU/RAM/GPU/network sampling), `BoostScoreCalculator`
- **History/** — `TweakHistory` (JSON file in `%LOCALAPPDATA%\PrimeOSTuner\history.json`) with append-only entries and undo support
- **Pipeline/** — `OneClickOptimizer` orchestrates running multiple tweaks with progress reporting

### Layer 3 — `PrimeOSTuner.Win`

- **RegistryClient** — read/write registry with automatic value backup before writes
- **ServiceClient** — start/stop/disable Windows services via `ServiceController`
- **PackageClient** — uninstall AppX/Microsoft Store apps via `PackageManager` (Windows.Management.Deployment)
- **HardwareClient** — wraps LibreHardwareMonitorLib for CPU/GPU temps and per-core load
- **ProcessClient** — `EmptyWorkingSet`, process priority/affinity changes
- **RestorePointClient** — `SystemRestore.Create()` / `Restore()` via `Interop.SystemRestore`
- **PInvoke/** — declarations for `NtSetTimerResolution`, `EmptyWorkingSet`, etc.

### Layer 4 — `PrimeOSTuner.Tests`

xUnit project. ~20–40 tests covering:

- BoostScoreCalculator math
- Tweak history JSON serialization round-trips
- Each tweak's probe/apply/revert with a mocked Win layer
- BloatwareCatalog list correctness

UI is **not** unit-tested in v1; manual testing on a VM (see §7) covers it.

### Data flow

```
User clicks "Optimize" in DashboardView
  -> ViewModel calls OneClickOptimizer.RunAsync()
    -> Optimizer iterates safe ITweak list
      -> Each tweak calls into Win layer
        -> Registry / Service / Package APIs execute
      -> Tweak returns TweakResult with undoData
      -> History records entry
    -> Optimizer reports progress via IProgress<T>
  -> ViewModel updates UI in real time
```

---

## 6. Safety rules (non-negotiable for every feature)

1. **Auto restore point** — Before any destructive batch, call `RestorePointClient.Create()`. If it fails, show a yellow warning and require explicit "I understand, continue" click.
2. **Per-tweak rollback** — Every `ITweak.ApplyAsync` returns undo data; every tweak implements `RevertAsync` consuming that data. The history file is the source of truth.
3. **Manual selection rule** — Anything destructive renders as a checklist with **everything unchecked**. User opts in per item. The "One-Click Optimize" button is restricted to safe-by-default tweaks (items 5–8) and explicitly does not include bloatware or game tweaks.
4. **Surgical elevation** — App launches as a normal user. When a tweak with `RequiresElevation = true` is applied, we relaunch a small helper process via UAC for that operation, then return. The full app does not run elevated.
5. **Try/catch every Win-layer call** — All exceptions are caught at the `ITweak` boundary, logged via Serilog, and returned as `TweakResult.Failure(message)`. No partial-application states.
6. **Dry-run preview** — Every tweak's `PreviewAsync` returns a human-readable description of what will change ("Will set `HKCU\Control Panel\Mouse\MouseSpeed` from `1` to `0`"). The UI exposes a "Preview" button next to "Apply" everywhere.
7. **No silent network calls** — v1 has no telemetry, no analytics, no auto-update check (auto-update added later via Velopack with explicit user consent).

---

## 7. Testing strategy

- **Unit tests (xUnit)** — Core logic only. Win layer is mocked via interfaces. ~20–40 tests for v1.
- **Manual testing on a Windows 11 VM** — Primary integration test environment. Snapshot before each test run, apply tweak, observe, restore snapshot. VirtualBox or Hyper-V (free).
- **Real-machine smoke testing** — Only after VM testing passes. Used to catch driver-specific or hardware-specific issues that don't reproduce in a VM.
- **No CI/CD for v1** — Local builds only. GitHub Actions will be added when shipping publicly.

---

## 8. Verification (how we'll know v1 is done)

- All 18 features in §4 are implemented and pass their unit tests
- Each feature has been exercised end-to-end on a clean Windows 11 VM with snapshot-restore between tests
- One-Click Optimize completes successfully on a fresh Windows 11 install with no errors logged
- Every destructive tweak creates a restore point and is reversible via the history tab
- App launches, runs, and exits cleanly as a normal user (does not require admin to start)
- Velopack-built installer installs cleanly and the app launches from Start Menu
- Manual smoke test on a real machine shows no regressions, no leaked processes, no excessive RAM/CPU usage by the app itself

---

## 9. Critical files (when implementation begins)

These are the files that define the v1 contract. Most other files derive from these.

- `src/PrimeOSTuner.Core/Tweaks/ITweak.cs` — the tweak interface every feature implements
- `src/PrimeOSTuner.Core/Pipeline/OneClickOptimizer.cs` — orchestrates the headline action
- `src/PrimeOSTuner.Core/History/TweakHistory.cs` — undo / audit log
- `src/PrimeOSTuner.Win/RestorePointClient.cs` — the safety-net wrapper
- `src/PrimeOSTuner.UI/MainWindow.xaml` — sidebar nav + page host
- `src/PrimeOSTuner.UI/Views/DashboardView.xaml` — the home tab (the most-seen surface)
- `src/PrimeOSTuner.UI/Theme/Colors.xaml` — the Hone-look color tokens
