---
id: disable-nic-power-management
title: Disable network adapter power saving
category: Network
difficulty: Beginner
risk: Low
time: 5 minutes
---
## What this does

Windows can switch your network adapter into a low-power state when it thinks the link is idle. On a wired connection that can add small, occasional latency spikes when the adapter wakes back up. Turning it off keeps the adapter at full power.

## Expected benefit

- Removes the occasional latency spike caused by the adapter powering down
- The effect is small and only shows up on some adapters. Honest expectation: this is a consistency tweak, not an FPS tweak.
- On a laptop running on battery, this slightly increases idle power draw

## Prerequisites

- A wired Ethernet connection (this matters less on Wi-Fi, which has its own power settings)

## Steps

1. Press the **Windows key + X** and choose Device Manager.
2. Expand **Network adapters**.
3. Right-click your Ethernet adapter and choose Properties.
4. Open the **Power Management** tab.
5. Uncheck **Allow the computer to turn off this device to save power**.
6. Click OK.

## How to verify it worked

- Reopen the adapter's Properties and the Power Management tab. The checkbox should still be unchecked.

## How to revert

- Same place: re-check "Allow the computer to turn off this device to save power" and click OK.

## Common pitfalls

- If the adapter's Properties has no Power Management tab, your driver does not expose this setting - there is nothing to do, and nothing is wrong.
- This is per-adapter. If you use more than one (for example Wi-Fi and Ethernet), repeat it for each.
