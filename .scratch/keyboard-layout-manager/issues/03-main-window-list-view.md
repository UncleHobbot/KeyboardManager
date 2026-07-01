# 03 — Main window: flat layout list + actions

Status: done

## What

Implement the main UI (FR-1) on top of issue 02's inspector:

- A `DataGrid` / `ListView` showing one row per `LayoutEntry` with columns:
  - **Status** — badge: Ghost / Declared / Orphan (with icon/color).
  - **Layout** — `{Language} — {Layout Text} ({id})`.
  - **Sources** — comma-joined registry locations.
- Default sort: Ghosts first, then Declared, then Orphans.
- Multi-select enabled.
- Action buttons: **Refresh**, **Remove selected**, **Reset to default**, **Backup now**.
- Shows the currently-configured default set next to the Reset button (read from config per FR-4).

Wires buttons to no-ops for now (filled by later issues) except Refresh, which
re-runs the inspector.

## Why

This is the surface the user lives in. Getting the list right validates the
inspection model and unlocks the destructive operations.

## Acceptance

- On launch, the list populates from the inspector.
- Ghosts appear at the top, clearly badged.
- Refresh re-reads and re-renders.
- Buttons are present and enabled/disabled sensibly (Remove disabled when nothing
  selected).
