# ADR-0001 — Elevate on demand, not via an always-elevated manifest

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

Writing to `HKU\.DEFAULT\Keyboard Layout\Preload` (`HKEY_USERS\S-1-5-18`) requires
administrator rights. Writing to `HKCU\Keyboard Layout\...` does not. Most of what
this tool does — viewing layouts, the HKCU Reset, removing layouts that only live
in HKCU — needs no elevation at all. Only targeted removal of a ghost that lives in
`.DEFAULT\Preload` needs admin.

Two viable approaches:

1. **Always-elevated manifest** (`requireAdministrator`). Every launch triggers UAC,
   even when the user only wants to look at their layouts or reset HKCU.
2. **Elevate-on-demand.** Launch non-elevated. Re-launch an elevated helper (or the
   app itself with a flag) only for the specific write that needs `.DEFAULT`.

## Decision

Elevate **on demand**. The main WPF process runs without elevation. When an
operation must touch `HKU\.DEFAULT\...\Preload`, the app spawns an elevated helper
to perform exactly that write, then returns to the non-elevated UI.

## Consequences

- **Better UX:** viewing layouts, Reset, and HKCU-only removals proceed without any
  UAC prompt — the common case is friction-free.
- **Honest prompting:** a UAC dialog appears only when the user's action genuinely
  requires system-hive access, so the prompt is meaningful rather than ritual.
- **Implementation cost:** the codebase splits into a UI process and an elevated
  worker, communicating the operation (and result) across the privilege boundary.
  This is more plumbing than a single always-elevated binary.
- **Risk:** if the elevation handoff is implemented carelessly, an elevated helper
  could be induced to write arbitrary values. The helper must accept only a narrow,
  validated operation description — not free-form registry paths.

## Alternatives considered

- **Always-elevated manifest:** simpler implementation, but a UAC prompt on every
  launch including HKCU-only work, which is the bulk of usage. Rejected as too
  noisy for the common case.
- **Never elevate, exclude `.DEFAULT`:** ghost layouts rooted in `.DEFAULT` could
  not be removed, defeating a primary purpose of the tool. Rejected.
