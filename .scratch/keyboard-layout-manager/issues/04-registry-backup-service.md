# 04 — Registry backup service

Status: done

## What

Implement the safety net that every destructive op depends on (the first layer of
the three-layer safety model):

- A `BackupService` that exports given registry keys to a `.reg` file via
  `reg export <key> <file> /y` (or direct registry enumeration + REGEDIT4 format
  writing as a fallback).
- Writes to `./backups/<yyyyMMdd-HHmmss>-<operation>.reg`.
- Returns the backup file path so the caller can surface it in the UI.

## Why

Every Remove and Reset must produce a restorable backup before touching the
registry. This is shared infrastructure for issues 05 and 06.

## Acceptance

- Given a key path, produces a valid `.reg` file that `reg import` (or double-click)
  restores.
- File name is unique and timestamped.
- Never throws silently — on failure, aborts the calling operation and reports.
