# Crustcut

A Windows PC performance optimizer for gamers — built in C# / .NET 9 / WPF.

Crustcut applies real, reversible Windows tweaks, detects your installed games, and
tracks in-game performance. Every optimizer maps to an actual Windows setting (registry,
power scheme, or service) — no placebo values. Each one can be toggled off and is restored
from a saved backup.

## Features

- **Optimize** — 35+ tweaks across FPS/latency, network, system, privacy, and power. Each
  tile shows what it does, whether it needs a reboot, and any risk note. Toggling re-applies
  or reverts the exact change, and choices are re-enforced on launch if Windows reverts them.
- **Game library** — detects games from Steam, Xbox, Ubisoft Connect, EA / Origin, Epic, and
  GOG, with cover art.
- **Sentinel** — watches a game while you play and shows the session's Average / Highest /
  1% Low FPS afterward (via PresentMon).
- **Performance overlay** — an optional, see-through in-game OSD (FPS / CPU / GPU / RAM /
  VRAM). Repositionable with a global hotkey; only shows while a game is running.
- **Memory trim**, **bloatware removal**, and an **optimization guide**.

## Requirements

- Windows 10 / 11 (x64)
- Administrator rights (the app self-elevates) for system-level tweaks

## Install (release)

Download `crustcut-vX.Y.Z-win-x64.zip` from
[Releases](https://github.com/Jescov-cmd/crustcut/releases), unzip anywhere, and run
`Crustcut.exe`. It's self-contained — no .NET install required.

## Build from source

```sh
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj -c Release
dotnet test  src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

Publish a self-contained single-file build:

```sh
dotnet publish src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o publish/out
```

## Project layout

| Project | Purpose |
| --- | --- |
| `PrimeOSTuner.Core` | Tweaks, profiles, monitoring, game registry, performance/Sentinel logic |
| `PrimeOSTuner.Win`  | Windows interop — registry, power plans, hardware counters, launchers |
| `PrimeOSTuner.UI`   | WPF app (views, view-models, services) |
| `PrimeOSTuner.Tests`| xUnit test suite |

Most registry tweaks are data-driven from
`src/PrimeOSTuner.Core/Tweaks/catalog/tweaks.json` — adding one is usually just a JSON entry.

## Safety

Every tweak is reversible and backs up the previous value before changing it. Tweaks that
require a reboot or carry a trade-off are labeled. Crustcut never auto-applies destructive
actions — bloatware removal and similar are opt-in per item.

## License

See [SECURITY.md](SECURITY.md) for the security policy.
