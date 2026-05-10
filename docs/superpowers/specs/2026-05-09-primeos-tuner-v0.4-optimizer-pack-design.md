# PrimeOS Tuner v0.4 — Optimizer Pack, Bloatware Tab & Memory Priority Tab

**Date:** 2026-05-09
**Status:** Spec — pending implementation plan
**Scope:** One large release. Three major additions in a single version.

---

## 1. Goals

1. Port and extend Hone's optimizer set so PrimeOS Tuner ships with a comprehensive system-tweak library (~38 tweaks).
2. Give bloatware its own dedicated tab with safe per-app Disable / Uninstall actions.
3. Repurpose the existing Custom Mode tab into a **Memory Priority** tab that lets users pin per-app CPU priority, protect apps from RAM cleanups, and run a safe Game Booster cleanup on launch.
4. Differentiate from Hone with the per-app Memory Priority + Game Booster workflow and a curated, transparent bloatware UX.

## 2. Non-Goals

- **No in-game graphics tweaks.** PrimeOS does not change shadows, textures, anti-aliasing, render scale, or anything that visibly degrades the game's image quality. Optimizers are limited to system-level settings (services, registry, power, network).
- No automatic destructive operations. Every uninstall, registry change, or service stop is opt-in per item.
- No game-detection or game-launching responsibilities beyond what the existing GameLibrary tab already does.
- No port of Hone's UI — we are porting the *behavior* of its tweaks, not its presentation.

## 3. High-Level Architecture

### 3.1 Tweak storage strategy — data-driven hybrid

About 25 of the new tweaks are simple "set this registry value, undo on revert" operations. They are stored as rows in `tweaks.json` and executed by a single `RegistryTweak` class. The remaining ~13 tweaks have logic (scanning, multiple keys, service interactions) and remain as their own `ITweak` classes.

### 3.2 New file layout

```
src/PrimeOSTuner.Core/
├── Tweaks/
│   ├── RegistryTweak.cs               (NEW) data-driven, loads from catalog
│   ├── ServiceDisableTweak.cs         (NEW) wraps ServiceController
│   ├── UltimatePerformanceTweak.cs    (NEW) enables hidden power plan
│   ├── HibernationTweak.cs            (NEW) powercfg /h off
│   ├── BloatwareDetectionService.cs   (NEW) scans installed AppX
│   ├── BloatwareItem.cs               (NEW) record: Id, Name, Size, Tier, Status
│   ├── BloatwareDisableTweak.cs       (NEW) startup + service disable per app
│   ├── BloatwareUninstallTweak.cs     (NEW) Get-AppxPackage | Remove-AppxPackage
│   └── catalog/
│       ├── tweaks.json                (NEW) ~25 simple registry tweak defs
│       └── bloatware-list.json        (NEW) known AppX names + safety tiers
├── Memory/
│   ├── PriorityRule.cs                (NEW) record: ExePath, Priority, Protect, GameBooster, IsGame
│   ├── PriorityRuleStore.cs           (NEW) JSON persistence
│   ├── PriorityWatcher.cs             (NEW) WMI Win32_ProcessStartTrace consumer
│   └── SafeRamCleaner.cs              (NEW) less-invasive cleanup for Game Booster
└── Profiles/
    └── BuiltInProfiles.cs             (UPDATED) adds new tweaks to Performance + Aggressive

src/PrimeOSTuner.UI/Views/
├── BloatwareView.xaml{.cs}            (NEW)
├── MemoryPriorityView.xaml{.cs}       (NEW; replaces CustomModeView)
└── Dialogs/
    ├── AddPriorityRuleDialog.xaml{.cs}     (NEW) running-process picker / file browse
    ├── BloatwareUninstallWarningDialog.xaml{.cs} (NEW) risky-tier warning
    └── BulkApplyGamesDialog.xaml{.cs}      (NEW) confirms recommended-to-all-games
```

### 3.3 Refactor scope (existing tweaks)

Convert these existing simple tweaks into `tweaks.json` rows (delete their `.cs` files):

- `NagleAlgorithmTweak.cs`
- `NetworkThrottlingIndexTweak.cs`
- `MouseAccelTweak.cs`
- `SystemResponsivenessTweak.cs`
- `PerAppGpuPreferenceTweak.cs`

Keep these as custom classes (have logic):

- `TimerResolutionTweak`, `PowerPlanTweak`, `GameModeTweak`, `HwGpuSchedulingTweak`, `CpuCoreParkingTweak`, `RamCleanerTweak`, `DnsFlushTweak`, `WindowsUpdateCacheTweak`, `DriverHealthCheckTweak`, `DriverStoreCleanupTweak`, `SafeRegistryCleanupTweak`.

### 3.4 Optimize tab filter chips

Current: `All / FPS & Latency / Network`
v0.4: `All / FPS & Latency / Network / System / Privacy / Power` (6 chips)

Bloatware is **not** a chip — it lives in its own top-level tab.

### 3.5 Top-level navigation after v0.4

`Dashboard · Optimize · Game Boost · Game Library · Bloatware · Memory Priority · History · Settings`

(Was 7 tabs; now 8. `CustomMode` is removed; `Bloatware` and `MemoryPriority` are added.)

---

## 4. The Optimizer Catalog

### 4.1 FPS & Latency (8 total — 6 existing, 2 new)

| ID | Name | Status |
|---|---|---|
| core.timer-resolution | Timer Resolution | existing |
| core.hw-gpu-scheduling | Hardware-accelerated GPU Scheduling | existing |
| core.cpu-core-parking | Disable CPU Core Parking | existing |
| core.game-mode | Game Mode | existing |
| core.gpu-pref-high | Per-app GPU Preference (High Performance) | existing → migrating to RegistryTweak |
| core.system-responsiveness | System Responsiveness | existing → migrating to RegistryTweak |
| core.win32-priority-separation | Win32 Priority Separation (favor foreground) | NEW (RegistryTweak) |
| core.startup-delay | Disable explorer startup delay | NEW (RegistryTweak) |

### 4.2 Network (9 total — 2 existing, 7 new)

| ID | Name | Status |
|---|---|---|
| core.nagle-algorithm | Disable Nagle's Algorithm | existing → migrating to RegistryTweak |
| core.network-throttling-index | Network Throttling Index | existing → migrating to RegistryTweak |
| core.tcp-ack-frequency | TCP ACK Frequency = 1 | NEW (RegistryTweak) |
| core.tcp-delivery-acceleration | TCP Delivery Acceleration | NEW (RegistryTweak) |
| core.qos-bandwidth | Disable reserved QoS bandwidth | NEW (RegistryTweak) |
| core.dns-prefetching | Tune DNS prefetching | NEW (RegistryTweak) |
| core.netbios-disable | Disable NetBIOS over TCP/IP | NEW (RegistryTweak) |
| core.ipv6-disable | Disable IPv6 (off by default — risk warning) | NEW (RegistryTweak) |
| core.nic-power-mgmt | Disable NIC power management | NEW (custom — touches device driver props) |

### 4.3 System (7 total — 1 existing, 6 new)

| ID | Name | Status |
|---|---|---|
| core.mouse-accel | Disable Mouse Acceleration | existing → migrating to RegistryTweak |
| core.sysmain-disable | Disable Superfetch / SysMain (good for SSDs) | NEW (ServiceDisableTweak) |
| core.search-indexing-tune | Windows Search → selective indexing | NEW (ServiceDisableTweak) |
| core.werror-reporting | Disable Windows Error Reporting | NEW (RegistryTweak) |
| core.connected-user-experiences | Disable Connected User Experiences (telemetry svc) | NEW (ServiceDisableTweak) |
| core.game-dvr-disable | Disable Game DVR / background recording | NEW (RegistryTweak) |
| core.fullscreen-optimizations | Disable fullscreen optimizations (system-wide) | NEW (RegistryTweak) |

### 4.4 Privacy (8 — all new)

| ID | Name |
|---|---|
| core.telemetry-disable | Disable telemetry (DiagTrack service + AllowTelemetry=0) |
| core.ceip-disable | Disable Customer Experience Improvement Program |
| core.activity-history | Disable Activity History |
| core.advertising-id | Disable Advertising ID |
| core.cortana-disable | Disable Cortana |
| core.location-tracking | Disable Location tracking |
| core.feedback-diagnostics | Disable Feedback & Diagnostics |
| core.typing-personalization | Disable typing/inking personalization |

All RegistryTweak except `core.telemetry-disable` and `core.cortana-disable`, which are custom (touch services + multiple registry keys).

### 4.5 Power (6 — all new)

| ID | Name |
|---|---|
| core.ultimate-performance | Enable Ultimate Performance power plan (custom: powercfg /duplicatescheme) |
| core.hibernation-disable | Disable Hibernation (custom: powercfg /h off) |
| core.usb-selective-suspend | Disable USB selective suspend (RegistryTweak) |
| core.pcie-aspm-disable | Disable PCIe link-state power management (RegistryTweak) |
| core.power-throttling-disable | Disable Power Throttling (RegistryTweak) |
| core.modern-standby-disable | Disable Modern Standby (force S3 sleep) (RegistryTweak) |

### 4.6 Risk-flagged tweaks

These display an inline warning badge in the tile because they can cause issues for some users:

- `core.ipv6-disable` — "May break some apps and VPNs."
- `core.nic-power-mgmt` — "Slightly higher idle power on laptops."
- `core.modern-standby-disable` — "May affect resume-from-sleep behavior."
- `core.search-indexing-tune` — "Reduces Start menu search speed."

`IsDestructive=false` on all of them (they're reversible), but the UI surfaces the warning before apply.

---

## 5. Bloatware Tab

### 5.1 Layout

A single scrollable view with:

- Header: detected count + estimated reclaimable size
- Per-item card with: icon, friendly name, AppX package id, size, safety-tier badge (✅ / ⚠️ / 🔒), Disable button, Uninstall button, Status pill (ON / Disabled / Uninstalled)
- "Refresh scan" button at the bottom

### 5.2 Detection

`BloatwareDetectionService.ScanAsync()`:

1. Loads `bloatware-list.json` (curated catalog of known bloatware AppX names + safety tier + friendly metadata).
2. Calls `Get-AppxPackage` via PowerShell to enumerate installed AppX packages.
3. Joins (2) against (1). Items present in both → reported.
4. For each match: queries `Get-AppxPackage` for `InstallLocation` and approximates size from folder enumeration (best-effort; falls back to "—" if unavailable).
5. Returns `BloatwareItem` records sorted by safety tier (Safe → Risky → Blocked) and size (largest first within tier).

### 5.3 Safety tiers

Each entry in `bloatware-list.json` carries a tier:

- **Safe** ✅ — Pure consumer apps with no system dependency. Examples:
  Candy Crush variants, Solitaire Collection, Get Help, Tips, Movies & TV, Maps, Mixed Reality Portal, Skype (preinstalled), Feedback Hub.
  Uninstall confirmation: simple "Are you sure?"

- **Risky** ⚠️ — Apps that some games or workflows depend on. Examples:
  Xbox Game Bar, Cortana, Photos, Camera, Your Phone.
  Uninstall confirmation: warning dialog with the *specific* risk note from the catalog (e.g., "Xbox Game Bar provides the in-game overlay for some games and Game Mode integration. Disabling is reversible — uninstalling is not.").

- **Blocked** 🔒 — Apps required by Windows shell or other apps. Examples:
  Microsoft Store, Microsoft Edge, Settings, Shell Experience Host, Start menu experience.
  Uninstall button is disabled; tooltip explains why. Only Disable is available.

### 5.4 Disable behavior

Per item:

1. Remove autostart entries (HKCU/HKLM\...\Run + Startup folder shortcuts + scheduled tasks tagged with the package name).
2. Stop and disable any service whose name matches the package id pattern.
3. For services we explicitly know about (e.g., `XblGameSave` for Xbox), set startup to Disabled.
4. Do not modify the AppX install — fully reversible by clicking Disable again, which re-enables.

### 5.5 Uninstall behavior

For Safe and Risky tiers:

1. Show confirmation dialog with risk note (Risky) or simple confirm (Safe).
2. On confirm: `Get-AppxPackage <id> | Remove-AppxPackage` (current user) — followed by `Remove-AppxProvisionedPackage -Online` (so it doesn't reappear for new accounts).
3. Update Status pill to "Uninstalled".
4. Refresh scan.

Blocked tier: button disabled; uninstall path never reached.

### 5.6 Persistence

The Bloatware tab is stateless — every visit re-scans the system. Disable state is reflected by inspecting registry / service state at scan time, not stored in a JSON file.

---

## 6. Memory Priority Tab

Replaces the existing `CustomModeView`.

### 6.1 What a rule contains

```csharp
public sealed record PriorityRule(
    string ExePath,                  // e.g., C:\Steam\...\cs2.exe
    string DisplayName,              // friendly, editable
    ProcessPriorityClass Priority,   // High / AboveNormal / Normal / BelowNormal (no Realtime)
    bool ProtectFromRamCleanup,
    bool GameBooster,                // run SafeRamCleaner 2s after launch
    bool IsGame                      // tagged from GameLibrary
);
```

### 6.2 Layout

- Header: title + subtitle
- Filter chips: `All / Games / Apps` (gradient when checked, same style as Optimize chips)
- "+ Add App" button
- "⚡ Apply Recommended to All Games" button (disabled when no detected games are missing rules)
- Sectioned list when chip is "All": `── GAMES ──` header, then `── APPS ──` header
- Per-rule card: icon, EXE name, friendly name, priority dropdown, two checkboxes (Protect, Game Booster), live status pill (● Live / Idle / ⚠ Failed), Remove button

### 6.3 Add App dialog

Two tabs:
- **Running processes** (default): list of currently-running EXEs with icons, file path, click row → adds.
- **Browse**: standard `OpenFileDialog` filtered to `*.exe`.

After adding, default values:
- Priority = `Normal`
- Protect = `false`
- Game Booster = `false`
- IsGame = `true` if the EXE matches a path from GameLibrary, else `false`

### 6.4 Apply Recommended to All Games

Confirmation dialog enumerates games in GameLibrary that don't already have a rule. On confirm, bulk-creates rules with:
- Priority = `High`
- Protect = `true`
- Game Booster = `true`

Existing user rules are not overwritten.

### 6.5 PriorityWatcher

Hosted singleton, owned by `App`. Subscribes to WMI `Win32_ProcessStartTrace` (also `Win32_ProcessStopTrace` for status updates).

On a process-start event:
1. Look up rules by `ExePath` (case-insensitive).
2. If a match found:
   - Set `Process.PriorityClass`. On failure (access denied), update status pill to `⚠ Failed`.
   - If `GameBooster == true`: schedule `SafeRamCleaner.RunAsync(launchedPid, protectedPids)` to run after a 2000 ms delay.
   - Update rule status to `● Live`.

On a process-stop event:
- Update rule status to `Idle`.

The watcher MUST be cancellable on app shutdown — WMI ManagementEventWatchers are disposed cleanly to avoid lingering threads.

### 6.6 SafeRamCleaner

Lower-impact cleanup designed not to glitch the launching app:

1. **Skip the launching PID and all PIDs in the protect list.** Compute exclusion set up-front.
2. **Skip system-critical PIDs** — `System` (PID 4), `csrss`, `wininit`, `services`, `lsass`, `winlogon`, `dwm`, `audiodg`, `explorer`.
3. **Use system file cache flush** — `SetSystemFileCacheSize(-1, -1, 0)` to release the file cache without per-process working-set trimming. (The existing aggressive `EmptyWorkingSet`-on-everything path stays in `RamCleanerTweak` for the manual "Optimize now" flow but is **not** what Game Booster invokes.)
4. **Working-set trim only on heavy non-protected background apps** — limit to processes using >100 MB working set, excluding the protect/game/system sets above.

This gives a meaningful pre-game RAM bump without the "first 5 seconds of a game look glitchy" problem.

### 6.7 Persistence

`PriorityRuleStore` reads/writes `%LOCALAPPDATA%\PrimeOSTuner\priority-rules.json`.

### 6.8 Integration with existing RAM cleaner

`RamCleanerTweak.ApplyAsync` accepts an optional protect-list. Both manual cleanup (Settings → "Optimize now") and threshold-/interval-based auto-cleanup honor the Memory Priority tab's protect list. Implementation: `RamCleanerTweak` queries `PriorityRuleStore` for protected EXE names and excludes their PIDs from `EmptyWorkingSet` calls.

---

## 7. Built-in Profiles (BuiltInProfiles.cs)

- **Basic** (gentle): adds `core.win32-priority-separation`, `core.startup-delay`, `core.werror-reporting`, `core.activity-history`, `core.advertising-id`. Stays away from anything risk-flagged.
- **Performance**: adds all FPS & Latency, all Network (except `core.ipv6-disable`), all System (except `core.search-indexing-tune`), all Privacy (except `core.cortana-disable`), `core.usb-selective-suspend`, `core.power-throttling-disable`, `core.ultimate-performance`.
- **Aggressive**: Performance + `core.ipv6-disable`, `core.cortana-disable`, `core.search-indexing-tune`, `core.modern-standby-disable`, `core.hibernation-disable`. Bloatware tab is **not** referenced from any profile (per-item only by user choice).

---

## 8. Migration (v0.3 → v0.4)

On first launch of v0.4:

1. If `%LOCALAPPDATA%\PrimeOSTuner\custom-mode.json` exists, copy it to `custom-mode.json.bak.v0.3`. The old custom-mode tab data is preserved for the user but no longer surfaced in the UI.
2. The new `priority-rules.json` is created empty.
3. Existing tweak undo data is unaffected (refactored tweaks keep the same `Id` strings, so the history table continues to resolve them).

---

## 9. Testing

### 9.1 Unit tests (xUnit, in `PrimeOSTuner.Tests`)

- `RegistryTweakTests` — parses `tweaks.json` rows, validates each row's `Probe`/`Apply`/`Undo` against an in-memory registry mock.
- `PriorityRuleStoreTests` — round-trip save/load, empty file, malformed JSON, schema upgrades.
- `BloatwareDetectionServiceTests` — given a mock AppX list, classifies tiers correctly, sorts as expected.
- `SafeRamCleanerTests` — given a mock process list, exclusion logic skips the right PIDs.
- `BuiltInProfilesTests` — Performance and Aggressive profiles include only IDs that exist in the catalog (no typos / dangling references).

### 9.2 Manual smoke tests

- Launch Notepad with a Memory Priority rule (Priority = High, Protect = true, Game Booster = true). Verify priority gets applied within ~1 s, status pill goes Live, RAM cleaner runs after 2 s, Notepad's working set is *not* trimmed.
- Open Bloatware tab, hit Refresh scan, confirm at least 5 entries appear, confirm Microsoft Store has a 🔒 badge and disabled Uninstall button.
- Apply a tweak from each chip filter on Optimize tab, reboot if required, confirm undo works after.
- Confirm Apply-Recommended-to-All-Games picks up only games already in Game Library.

---

## 10. Open questions / deferred decisions

- Whether to surface a "scan for new games" button in Memory Priority that re-runs Game Library detection. (Deferred — for now, user goes to Game Library tab to refresh, then comes back.)
- Whether to add per-rule custom hotkeys (e.g., Ctrl+Alt+G to manually trigger Game Booster cleanup). Deferred to v0.5.
- Whether the bloatware tab should expose a "Disable all Safe items" bulk action. Deferred — explicit per-item is the safer default given user's stated rule about destructive actions.

---

## 11. Risk register

- **Risk:** PowerShell-based AppX enumeration is slow on first call (~1-2 s).
  **Mitigation:** Show "Scanning…" skeleton, run on a background thread, cache results until "Refresh scan".
- **Risk:** WMI process-start events have a small delay (50-200 ms typical, occasional 1+ s).
  **Mitigation:** Acceptable. Game Booster's 2-second post-launch delay absorbs this; priority application happens "fast enough" for users.
- **Risk:** `Remove-AppxProvisionedPackage` requires admin.
  **Mitigation:** PrimeOS already runs elevated (it has to, for power plans, services, registry). Confirm in implementation; otherwise prompt UAC for the uninstall step only.
- **Risk:** A user uninstalls Microsoft Photos and breaks their default image viewer.
  **Mitigation:** Microsoft Photos is in the Risky tier; the warning dialog explicitly notes this.
