# 06 — Reset operation

Status: ready-for-agent

## What

Implement FR-3, wired to the **Reset to default** button:

1. Back up `HKCU\Keyboard Layout\Preload` and `Substitutes` (issue 04).
2. Clear both keys completely.
3. Write a clean Preload from the configured default set (FR-4): by default
   `{1: 00000409, 2: 00000419}`. Substitutes left empty.
4. Do **not** touch `HKU\.DEFAULT\...\Preload`.
5. Best-effort apply + advise sign-out/sign-in.
6. Confirmation dialog before execution showing the target set.

Also implement FR-4 config loading (`KeyboardManager.config.json` next to the exe,
fallback `%APPDATA%\KeyboardManager\config.json`) with the documented default
content.

## Why

One-click recovery to a known-good state. Complements targeted Remove for users
who just want a clean slate.

## Acceptance

- Reset clears HKCU Preload/Substitutes and writes exactly the configured default.
- `.DEFAULT` is untouched.
- A `.reg` backup of the prior HKCU state is produced.
- If config is missing/malformed, falls back to the built-in default and notes it
  in the UI.
