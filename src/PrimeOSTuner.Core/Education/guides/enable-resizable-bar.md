---
id: enable-resizable-bar
title: Enable Resizable BAR
category: BIOS / UEFI
difficulty: Intermediate
risk: Medium
time: 10-15 minutes, requires restart
---
## What this does

Allows your CPU to access your entire GPU VRAM at once, instead of in small 256 MB chunks. This is a real PCIe feature, not a setting that just relabels existing behaviour.

## Expected benefit

- 5-15% FPS gain in some games, especially CPU-bound, high-framerate scenarios
- No change at all in many games
- NVIDIA keeps a per-game whitelist in the driver. Even with ReBAR enabled in hardware, only tested games use it, so you will not see regressions when it is not helping.

## Prerequisites

- GPU: RTX 30-series or newer
- CPU: Intel 10th gen or newer, or AMD Ryzen 3000 or newer
- A motherboard BIOS that supports ReBAR (most boards from 2021 onward)
- Save any open work first. This requires entering the BIOS and restarting.

## Steps

1. Check the current status first. Open the NVIDIA Control Panel, then Help, then System Information, and look for "Resizable BAR". If it already says Yes, you are done.
2. Restart your PC and enter the BIOS. Common keys are Delete, F2, or F11 during boot; on MSI boards it is usually Delete. You may need to disable Fast Boot in Windows first if the BIOS will not catch your keypress.
3. Enable **Above 4G Decoding**. This is usually under Advanced, then PCIe Subsystem Settings (or PCI Configuration). The exact path varies by board. Set it to Enabled.
4. Enable **Re-Size BAR Support**. It is in the same menu area, usually right below Above 4G Decoding. Set it to Auto or Enabled.
5. Save and exit the BIOS, usually with F10.
6. Boot back into Windows.

## How to verify it worked

- Reopen the NVIDIA Control Panel, then Help, then System Information. "Resizable BAR" should now say Yes.
- Or run GPU-Z (free): the main tab shows "Resizable BAR: Enabled".

## How to revert

- Go back into the same BIOS menu and set both options back to Disabled.
- ReBAR has no Windows-side toggle. It is controlled purely in hardware and the BIOS.

## Common pitfalls

- Above 4G Decoding must be enabled first, or Re-Size BAR Support will not be available.
- Some older BIOS versions need a BIOS update before ReBAR appears as an option. Check your motherboard manufacturer's website.
- Very rarely, enabling ReBAR causes boot issues with old GPUs. If that happens, clear CMOS to restore defaults.
