# 08 — Manual backup action

Status: ready-for-agent

## What

Implement FR-5, wired to the **Backup now** button:

- Export all three sources (`HKCU\Preload`, `HKCU\Substitutes`, `HKU\.DEFAULT\Preload`)
  to a single timestamped `.reg` file via issue 04's `BackupService`.
- For `.DEFAULT`, this requires the elevation path (issue 07) — or skip `.DEFAULT`
  with a clear note if the user declines UAC.
- Show the resulting file path in the UI.

## Why

Lets the user snapshot state before experimenting, with no changes applied. A
trust-building feature for a tool that edits the registry.

## Acceptance

- Clicking Backup now produces one `.reg` file containing all three sources.
- The file path is shown to the user.
- If the user declines UAC for the `.DEFAULT` portion, the HKCU portion still
  backs up and the UI notes the omission.
