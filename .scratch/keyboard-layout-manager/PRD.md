# PRD — Keyboard Layout Manager

A small Windows 11 WPF tool to find and remove **ghost keyboard layouts** — layouts that appear in the Win+Space switcher but are invisible in Settings and cannot be removed via the normal UI.

## Problem

Windows loads keyboard layouts from multiple registry sources. The Settings UI reads only some of them. Layouts that load from sources Settings ignores ("ghosts") show up in the Win+Space switcher, interfere with typing, and cannot be removed through any built-in UI. The only fix today is manual registry editing, which is error-prone.

## Goals

- Show the **real, active** set of layouts — what the user actually experiences — alongside each layout's registry sources.
- Let the user remove a specific layout from **every** source it lives in, including `HKU\.DEFAULT\...\Preload`.
- Provide a one-click **reset** to a known-good default set.
- Make every destructive operation safe: automatic `.reg` backup, concrete confirmation, honest post-op guidance.

## Non-goals

- Adding arbitrary layouts (Windows Settings already does this well).
- General-purpose product polish: installer, code signing, localization.
- Touching the logon/welcome screen layout as part of reset (that's `.DEFAULT`, handled only via targeted removal).

## Personas / audience

Personal tool, clean enough to share. Primary user is the developer; secondary is a technically-comfortable friend given the `.exe`.

## Functional requirements

### FR-1 — View active layouts
On launch and on Refresh, display every **active** layout (from `GetKeyboardLayoutList`) as a flat list. For each row:
- Status badge: **Ghost** (active, not declared), **Declared** (in HKCU Preload), or **Orphan** (dangling Substitutes entry).
- Display name in the form `{Language} — {Layout Text} ({layout id})`, resolved from `HKLM\...\Keyboard Layouts\<id>` with fallback to the raw hex ID.
- Sources: the list of registry locations this layout was found in (e.g. `HKCU\Preload#2`, `HKU\.DEFAULT\Preload#1`).

Sort: Ghosts first, then Declared, then Orphans.

### FR-2 — Remove a layout
User selects one or more layouts and chooses Remove. The tool:
1. Takes a `.reg` backup of every affected key into `./backups/<timestamp>-<operation>.reg`.
2. Shows a confirmation dialog listing exactly which values from which keys will be deleted (e.g. "Delete `00000422` (Ukrainian) from: HKCU\...\Preload#3, HKU\.DEFAULT\...\Preload#2").
3. On confirm, removes the layout from **all** sources where it is found, **including `.DEFAULT\Preload`**.
4. Cleans up any dangling `Substitutes` entries left behind.
5. Best-effort applies the change to the running session (`UnloadKeyboardLayout`, broadcast `WM_SETTINGCHANGE`).
6. Shows honest guidance: a sign-out/sign-in may be required for `.DEFAULT`-sourced ghosts to fully clear.

### FR-3 — Reset to default
User chooses Reset. The tool:
1. Backs up `HKCU\Keyboard Layout\Preload` and `Substitutes` to a `.reg` file.
2. **Clears** both keys completely.
3. Writes a clean Preload set from the configured default (`{1: 00000409, 2: 00000419}` by default); Substitutes left empty.
4. Does **not** touch `HKU\.DEFAULT\...\Preload`.
5. Best-effort applies + advises sign-out/sign-in.

### FR-4 — Default configuration
The reset target set is read from `KeyboardManager.config.json` next to the exe (fallback `%APPDATA%\KeyboardManager\config.json`). Default content:

```json
{
  "defaultLayouts": [
    { "id": "00000409", "name": "English (United States) — US" },
    { "id": "00000419", "name": "Russian — Russian" }
  ]
}
```

The UI shows the current configured default next to the Reset button so the user knows what Reset will produce.

### FR-5 — Manual backup
A "Backup now" action exports all three sources (`HKCU\Preload`, `HKCU\Substitutes`, `HKU\.DEFAULT\Preload`) to a timestamped `.reg` file without any changes.

### FR-6 — Elevation
The app launches **non-elevated** and operates freely on HKCU. Any operation that must touch `HKU\.DEFAULT\...\Preload` triggers a UAC prompt by re-launching an elevated helper (or self) to perform just that write. Operations limited to HKCU never trigger UAC.

## Success criteria

- On the developer's machine, the Win+Space switcher shows only the configured default set after running Reset and signing back in.
- A ghost layout can be removed with one Remove action + one confirmation.
- Every destructive operation produces a `.reg` backup file that, when double-clicked, restores the prior state.
- A friend can run the `.exe`, see their own layouts, and perform Reset/Remove without editing code.

## Open questions / risks

- `UnloadKeyboardLayout` cannot always evict a layout the session still considers in use; the sign-out fallback covers this but should be messaged clearly.
- HKLM layout-name resolution may miss layouts installed by non-standard means; hex-ID fallback prevents a crash but loses display names in rare cases.
