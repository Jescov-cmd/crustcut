---
id: clean-gpu-driver-install
title: Do a clean GPU driver install with DDU
category: GPU Driver
difficulty: Intermediate
risk: Medium
time: 20-30 minutes
---
## What this does

DDU (Display Driver Uninstaller) fully removes your current GPU driver and every leftover file, so a fresh driver install starts from a clean slate with nothing old to conflict with.

## Expected benefit

- Fixes driver-related stutter, crashes, black screens, and failed driver upgrades
- This is a repair tool, not an FPS tweak - if your driver is already healthy, expect no change
- Essential when switching GPU brands (NVIDIA to AMD or back)

## Prerequisites

- Download **DDU** (from Wagnardsoft) and the **latest driver for your GPU** before you start - you will briefly have no display driver
- Best done in Windows Safe Mode

## Steps

1. Download DDU and your new GPU driver now, while you still have a working driver.
2. Disconnect from the internet, so Windows does not auto-install a generic driver mid-process.
3. Boot into Safe Mode (Settings, then Recovery, then Restart now, then Troubleshoot, Advanced options, Startup Settings).
4. Run DDU. Pick GPU and your brand, then choose **Clean and restart**.
5. Back in normal Windows, run the driver installer you downloaded in step 1.
6. Reconnect the internet.

## How to verify it worked

- GPU-Z or Device Manager shows your new driver version.
- Device Manager shows no warning icon on the display adapter.

## How to revert

- There is nothing to undo - DDU only removes. If you want a different driver version, just install it normally.

## Common pitfalls

- Do not run DDU for every routine driver update. It is for fixing problems, not regular maintenance.
- Your screen resolution will look wrong until the new driver installs - that is normal.
- Download the new driver **first**. If you forget, you will be stuck with no driver and no internet.
