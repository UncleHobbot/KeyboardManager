# 02 — Layout inspection service

Status: done

## What

Build the read layer that powers FR-1. A `LayoutInspector` service that produces
the unified view model:

- Enumerate **active** layouts via P/Invoke `GetKeyboardLayoutList`.
- Read sources from the registry:
  - `HKCU\Keyboard Layout\Preload`
  - `HKCU\Keyboard Layout\Substitutes`
  - `HKU\.DEFAULT\Keyboard Layout\Preload`
- For each active layout, determine which sources reference it (resolving
  Substitutes mappings).
- Compute **status**: Ghost (active, not in HKCU Preload), Declared (in HKCU
  Preload), Orphan (dangling Substitutes entry referencing a layout not active and
  not in Preload).
- Resolve display names from `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\<id>`
  (`Layout Text`), with hex-ID fallback.

## Why

The UI list (FR-1) and every deletion decision depend on an accurate, joined view
of active layouts + their registry sources. This is the core domain logic.

## Acceptance

- Unit-testable: registry access behind an interface so tests can feed fake hives.
- Returns a list of `LayoutEntry { Status, DisplayName, LayoutId, Sources[] }`.
- On a machine with a known ghost, the inspector flags it as Ghost with the correct
  source list.
