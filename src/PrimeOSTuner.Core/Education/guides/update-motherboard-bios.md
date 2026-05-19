---
id: update-motherboard-bios
title: Update your motherboard BIOS
category: BIOS / UEFI
difficulty: Advanced
risk: High
time: 20-40 minutes
---
## What this does

A BIOS (UEFI) update replaces the firmware on your motherboard. It can add support for newer CPUs, improve memory stability (so XMP/EXPO is more reliable), and fix bugs.

## Expected benefit

- Mostly stability and compatibility - more reliable memory overclocking, newer CPU support, bug and security fixes
- It rarely changes FPS directly
- Honest: only update if you have a specific reason. This is not a routine tune-up.

## When NOT to update

- If your PC is stable and everything works, and the changelog does not mention something you actually need - **do not update**. "If it isn't broken, don't fix it" genuinely applies to BIOS firmware.

## Prerequisites

- Know your **exact** motherboard model and revision, and your current BIOS version
- A reliable power source - a power cut during the flash can permanently brick the board
- A USB stick

## Steps

1. Find your motherboard model and current BIOS version. They are shown in the BIOS itself, or in Windows via msinfo32.
2. On the manufacturer's website, download the BIOS for your **exact** model and revision.
3. Read the changelog for that version. Only continue if it fixes or adds something you need.
4. Format a USB stick as FAT32 and copy the BIOS file onto it.
5. Reboot into the BIOS and open the built-in flashing tool - M-Flash on MSI, EZ Flash on ASUS, Q-Flash on Gigabyte.
6. Select the file and start the flash. **Do not turn off or restart the PC until it finishes on its own.**
7. After it reboots, re-enable XMP/EXPO and any other settings - a BIOS update resets them to default.

## How to verify it worked

- The BIOS version shown in the BIOS screen, or in msinfo32, matches the version you flashed.

## How to revert

- Many boards let you flash an older BIOS the same way, and some have a dual-BIOS backup. But reverting is **not guaranteed** - that is exactly why you only update when you have a real reason.

## Common pitfalls

- Never interrupt power during the flash. This is the one step that can permanently kill the motherboard.
- Only ever use the BIOS file for your exact model and board revision.
- A BIOS update wipes your settings - your RAM will drop back to stock speed until you re-enable XMP/EXPO.
- If you have never done this before, watch a video walkthrough for your specific board first.
