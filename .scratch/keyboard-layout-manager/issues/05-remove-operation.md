# 05 — Remove operation (with confirmation + apply)

Status: ready-for-agent

## What

Implement FR-2 end to end, wired to the **Remove selected** button:

1. For each selected layout, compute the exact set of `{key, valueName}` pairs to
   delete across **all** sources it appears in — including `HKU\.DEFAULT\...\Preload`.
2. Trigger issue 04's backup over every affected key.
3. Show a confirmation dialog listing the concrete deletions, e.g.:
   "Delete `00000422` (Ukrainian) from: HKCU\...\Preload#3, HKU\.DEFAULT\...\Preload#2".
4. On confirm:
   - Delete the values.
   - Clean up dangling `Substitutes` entries referencing the removed layout.
   - If any target is under `.DEFAULT`, go through the elevation path (issue 07).
   - Best-effort apply: `UnloadKeyboardLayout` for the removed HKLs, broadcast
     `WM_SETTINGCHANGE`.
5. Show post-op guidance: a sign-out may be needed for `.DEFAULT`-sourced ghosts.

## Why

This is the tool's primary value — killing ghosts. It must be safe (backup +
concrete confirmation) and honest about apply limitations.

## Acceptance

- Selecting a ghost that lives in HKCU only and removing it: no UAC, layout
  disappears from Win+Space after Refresh (or after sign-out).
- Selecting a ghost rooted in `.DEFAULT`: UAC prompt appears, then removal proceeds.
- A `.reg` backup exists for every run and restores the prior state.
- Dangling Substitutes entries are removed in the same operation.
