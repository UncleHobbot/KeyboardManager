# 07 — Elevation helper for .DEFAULT writes

Status: done

## What

Implement the elevate-on-demand path from ADR-0001 for any write that must touch
`HKU\.DEFAULT\Keyboard Layout\Preload` (used by issue 05):

- A separate entry point / mode in the app (or a small helper project) that runs
  elevated and performs a narrowly-specified write operation, then exits.
- The main UI detects when an operation needs `.DEFAULT` access and launches this
  helper via `Process.Start(... , Verb = "runas")`, passing the operation as
  validated arguments (key path + value names to delete — never free-form paths).
- Helper validates its input, performs the write, writes to a result file / exit
  code the UI reads back, and exits.
- UAC prompt appears only at this moment.

## Why

ADR-0001's decision. Keeps the common case (HKCU-only) UAC-free while still
allowing `.DEFAULT`-rooted ghosts to be removed.

## Acceptance

- An HKCU-only Remove triggers no UAC.
- A Remove that needs `.DEFAULT` triggers exactly one UAC prompt and completes the
  write.
- The helper refuses unexpected/invalid arguments (no generic registry write
  surface).
- Result of the elevated write is surfaced back to the UI (success/failure).
