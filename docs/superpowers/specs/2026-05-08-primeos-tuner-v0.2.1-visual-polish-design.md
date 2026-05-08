# PrimeOS Tuner v0.2.1 — Visual Polish Pass

**Status:** Design approved 2026-05-08, ready for plan-writing.

**Goal:** Take the v0.2 app — functional but visually bare — and make it look like a premium gaming-grade performance tool. No feature changes, no behavior changes; purely cosmetic. Same 90 unit tests, same DI graph, same XAML structure where possible.

## Why now

v0.2.0 shipped feature-complete. The user's reaction after running v0.1: *"It did great but I'm guessing it's gonna look different in the future. Right now it just doesn't really look anything special or cool looking."* The aesthetic target on file is "Hone's premium gamer look — dark theme, animated stats, glowing accents — not a corporate utility look." This pass closes that gap.

## What v0.2.1 is not

- Not a feature pass. No new tweaks, no new tabs, no behavior changes.
- Not a refactor. Existing XAML structure stays; we modify resources and add storyboards.
- Not a font swap. Stays on Segoe UI (no licensing concerns, beginner-friendly).
- Not a control replacement. WPF-UI is adopted only for the window backdrop and a few primitives — the existing custom controls (BoostScoreRing, StatCard, GameCard) keep their structure.

## Foundation: WPF-UI Fluent

WPF-UI 3.0.5 is already in the csproj from v0.1 but unused. Adopt it for:

- **Window backdrop:** `WindowBackdropType="Mica"` on the main window, falls back gracefully on Win10.
- **Title bar:** WPF-UI's `TitleBar` control replacing the default Windows chrome — flat, themed, no system-grey buttons.

Everything else (buttons, inputs, scrollbars) stays on the existing custom styles to keep the diff small.

## Palette: cyan → crimson

The accent flips from `#00e5c5` (teal-cyan) to `#ff4d6d` (crimson). This is a brand identity shift — reads as "performance / overclocking tool" rather than "modern dashboard."

### Colors.xaml — what changes

| Key | Before | After | Notes |
|---|---|---|---|
| `AccentColor` | `#00e5c5` | `#ff4d6d` | Primary accent |
| `Accent2Color` | `#6ad7ff` (light blue) | `#ff8095` (warm pink) | Secondary, gradient pair |
| `AccentDeepColor` | (new) | `#d11a3e` | For gradient bottoms on buttons |
| `Bg0/1/2/3Color` | unchanged | unchanged | Dark navy stays |
| `Text0/1/2/3Color` | unchanged | unchanged | |
| `LineColor` | unchanged | unchanged | |
| `GoodColor` | `#43d27a` | unchanged | Status: green |
| `WarnColor` | `#ffb84d` | unchanged | Status: orange |
| `DangerColor` | `#ff6b6b` | unchanged | Different shade than accent — works alongside |

The `AccentBrush` keeps its name, just points at the new color. Every consumer (BoostScoreRing, "Apply Now" buttons, sidebar highlight, pill labels) updates automatically.

A new `AccentGradientBrush` (LinearGradientBrush from `AccentColor` to `AccentDeepColor`) is added for buttons and the score ring fill.

## Foundation effects

### Window
- Mica backdrop (Win11) via WPF-UI; subtle dark fallback on Win10.
- A faint radial gradient overlay at top-left (10% accent, 50% radius) — one `Border` overlay in MainWindow.xaml. Reads as "soft glow in the corner."

### Card style
`CardBorder` style gets:
- Subtle vertical gradient on background (`Bg2` → slightly darker `Bg2`).
- 1px inner highlight via inner `Border` with `BorderBrush="#10FFFFFF"`.
- `DropShadowEffect` (BlurRadius=20, Opacity=0.35, ShadowDepth=4, Color=Black).

### Sidebar
- Bg gets a radial accent glow at the top (small `Border` with `RadialGradientBrush`, 8% accent opacity).
- The `NavButtonStyle` adds a transition on `Background` and tightens letter-spacing on labels.

## Animations (six, all in)

All animations use WPF Storyboards triggered from XAML — no code-behind animation logic. Each is local to its control so it can be unit-tested by inspecting the visual tree.

### 1. Halo on the boost score ring
- Add a `Path` element behind the ring with a conic gradient sweep (transparent → accent 50% → transparent over 120°).
- Storyboard with `RotateTransform.Angle` from 0 to 360 over 4 seconds, repeating forever.
- Lives inside `BoostScoreRing.xaml`.

### 2. Count-up tweens on numbers
- Add a small `AnimatedNumber` user control: takes a `TargetValue` dependency property, exposes a `DisplayValue` (string) that tweens via `DoubleAnimation` on a backing double.
- BoostScoreRing's score number and StatCard's value text use `AnimatedNumber` instead of plain TextBlock.
- Tween duration: 800 ms with `CubicEase` out.
- On VM property change → tween from old to new value.

### 3. Pulse glow on primary buttons
- New style `PrimaryActionButton` extending current button style.
- Adds an `OuterGlow` Border behind the button content.
- Storyboard cycles `OuterGlow.Effect.BlurRadius` and `Opacity` every 2 seconds.
- A `DataTrigger` on `IsEnabled=False` stops the storyboard (so it doesn't pulse while applying).
- Applied to: Optimize page's "Optimize Now," GameBoost's three "Apply Now" buttons, Game Library's "+ Add Game" button.

### 4. Hover lift + glow on cards
- Card style adds `Triggers` for `IsMouseOver`:
  - `RenderTransform.TranslateY` from 0 to -4 (200 ms).
  - `DropShadowEffect.BlurRadius` from 20 to 32, `Color` to accent.
  - `BorderBrush` from line color to accent at 40% opacity.
- Applied to GameCard, GameBoost mode cards, OptimizeView tweak rows.

### 5. Sliding nav indicator
- New `Path` element in MainWindow sidebar — a 3px-wide rounded rect with vertical gradient.
- Position bound to a `NavSelectorViewModel.SelectedTabIndex`.
- A `DoubleAnimation` on `Canvas.Top` runs whenever the index changes (300 ms ease-out).
- The nav buttons no longer paint their own selected state — they just trigger an index change.

### 6. Glowing sparklines
- `StatCard.xaml`: wrap the LiveCharts2 `CartesianChart` in a `Border` with `DropShadowEffect` (Color=accent, BlurRadius=12, Opacity=0.6).
- Series stroke width bumped from 2 → 2.5 for a thicker glow.

## Typography

- Stay on Segoe UI. No font swap.
- `HeaderText` style: weight stays Bold, size goes from 20 → 24, letter-spacing slightly tightened.
- `SectionLabel` style: letter-spacing increased from 0 to 2px (the "tactical readout" feel).
- Score number in BoostScoreRing: weight Black (900), size 36, gradient brush as foreground.

## Scope

### Files modified

```
src/PrimeOSTuner.UI/
├── Theme/
│   ├── Colors.xaml          (palette flip + new gradient brushes)
│   └── Styles.xaml          (CardBorder gets gradient + shadow + hover triggers; new PrimaryActionButton style)
├── Controls/
│   ├── AnimatedNumber.xaml(.cs)   NEW
│   ├── BoostScoreRing.xaml         (halo storyboard + AnimatedNumber for the score)
│   ├── StatCard.xaml               (AnimatedNumber for the value, glowing sparkline wrapper)
│   └── GameCard.xaml               (hover trigger via shared CardBorder)
├── Views/
│   ├── DashboardView.xaml          (apply new card style; the active-profile panel gets a subtle ribbon)
│   ├── OptimizeView.xaml           (PrimaryActionButton on Optimize Now)
│   ├── GameBoostView.xaml          (PrimaryActionButton on three Apply buttons)
│   ├── GameLibraryView.xaml        (PrimaryActionButton on Add Game; GameCard hover already covered)
│   ├── CustomModeView.xaml         (PrimaryActionButton on Save)
│   └── HistoryView.xaml            (card style refresh only)
├── MainWindow.xaml(.cs)            (Mica backdrop + radial glow overlay + sliding nav indicator)
└── ViewModels/
    └── ShellViewModel.cs           (add SelectedTabIndex for nav indicator)
```

### Tests

- All 90 existing unit tests must still pass — no test deletion.
- Add a small ShellViewModel test asserting `SelectedTabIndex` updates when `NavigateTo("GameBoost")` is invoked.
- No XAML rendering tests (would need WPF test harness; cost-prohibitive).

### Estimated effort

~8 hours real time across 7-9 commits. One commit per logical chunk:
1. Palette flip (Colors.xaml)
2. Foundation: Window + Mica + sidebar glow
3. Card style overhaul (CardBorder + hover triggers)
4. AnimatedNumber control
5. BoostScoreRing halo + count-up
6. PrimaryActionButton + apply to all "Apply" buttons
7. Sliding nav indicator
8. StatCard glowing sparklines
9. Typography tightening pass

### Out of scope

- Custom fonts (Inter, Manrope, etc.) — would need licensing/loading
- Theme switcher (light mode, alternate accents)
- Custom titlebar buttons beyond what WPF-UI provides
- Replacing the LiveCharts2 sparkline with a hand-drawn alternative
- New tabs, new tweaks, new dialogs

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Mica backdrop fails on Win10 | WPF-UI handles fallback automatically; we test on Win11 host but the fallback is documented in WPF-UI docs |
| AnimatedNumber memory leak | DoubleAnimation completes and releases; we use `FillBehavior="Stop"` so no lingering animation clocks |
| Pulse storyboard on disabled button | `DataTrigger` on `IsEnabled=False` stops the storyboard cleanly |
| Sliding nav indicator gets out of sync if tabs added later | Index is bound, not hard-coded — adding a tab just appends |
| Color #ff4d6d clashes with the existing #ff6b6b DangerColor | They're used in different contexts (accent = brand, danger = error states) and the gradient pair distinguishes them visually |
| Existing screenshots/marketing reference cyan | None exist yet — v0.1 was internal only |

## Acceptance criteria

When v0.2.1 is "done":
1. `dotnet build` succeeds with 0 errors.
2. `dotnet test --filter "Category!=Integration&Category!=Network"` reports 91+ passed (90 prior + new ShellViewModel test).
3. Running the app shows: Mica backdrop on Win11, crimson accents throughout, animated halo on the score ring, pulse glow on at least one Apply button, hover lift on at least one card, sliding indicator on sidebar tab change, and glowing sparklines on at least one stat card.
4. v0.2.1.0 git tag created.

## Definition of "shipped"

Same bar as v0.2.0 — code committed, tagged, build runs cleanly when launched on the host. No VM smoke required since this is purely visual and doesn't touch registry/system state.
