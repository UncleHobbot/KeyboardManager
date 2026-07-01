# Keyboard Layout Manager — Agent Guide

A Windows 11 WPF tool for finding and removing **ghost keyboard layouts** — layouts that appear in the Win+Space switcher but are invisible in Settings and cannot be removed through any built-in UI.

## Quick orientation

| Want to… | Read this |
|---|---|
| Understand the domain (Active vs Declared vs Ghost layouts, sources, reset) | [`CONTEXT.md`](./CONTEXT.md) |
| Read the full spec | [`.scratch/keyboard-layout-manager/PRD.md`](./.scratch/keyboard-layout-manager/PRD.md) |
| See the implementation breakdown | [`.scratch/keyboard-layout-manager/issues/`](./.scratch/keyboard-layout-manager/issues/) |
| Understand why elevation works the way it does | [`docs/adr/0001-elevate-on-demand.md`](./docs/adr/0001-elevate-on-demand.md) |
| See the problem screenshots | [`docs/2026-06-21_*.png`](./docs/) |

## Tech stack

- **.NET 9** + **WPF** (target: `net9.0-windows`)
- Solution: `KeyboardManager.slnx`
- App project: `src/KeyboardManager/`
- Test project: `tests/KeyboardManager.Tests/` (xUnit)

## Build & run

```bash
dotnet build
dotnet run --project src/KeyboardManager/KeyboardManager.csproj
dotnet test
```

## Architecture

The code is split into read (inspection), write (removal/reset/backup), and runtime (apply) layers, all behind interfaces so the logic is unit-testable against an in-memory fake registry.

```
src/KeyboardManager/
├── Models/
│   ├── LayoutEntry.cs          # one resolved row in the UI list
│   ├── LayoutStatus.cs         # Ghost / Declared / Orphan enum
│   ├── LayoutSourceEntry.cs    # one registry location a layout was found in
│   └── LayoutSourceKind.cs     # HKCU/Default × Preload/Substitutes
├── Services/
│   ├── IKeyboardLayoutRegistry.cs          # read+write abstraction over the registry
│   ├── WindowsKeyboardLayoutRegistry.cs    # live implementation
│   ├── LayoutInspector.cs                  # joins sources → flat ghost-first list
│   ├── LayoutRemovalService.cs             # plans + executes a layout removal
│   ├── LayoutResetService.cs               # clears HKCU → configured defaults
│   ├── BackupService.cs                    # exports keys to .reg files
│   ├── SessionLayoutApplier.cs             # best-effort UnloadKeyboardLayout + broadcast
│   ├── Configuration/
│   │   └── KeyboardManagerConfig.cs        # loads the reset target from JSON
│   └── Elevation/
│       ├── ElevatedOperation.cs            # the narrow validated write contract
│       ├── ElevatedHelper.cs               # elevated-side executor
│       ├── ElevatedOperationRunner.cs      # non-elevated launcher (runas handoff)
│       └── ElevatedOperationJsonContext.cs # source-gen JSON for the handoff
├── ViewModels/
│   └── MainViewModel.cs
├── Converters/
│   └── StatusConverters.cs                 # Status→brush/label, Sources→text
├── App.xaml.cs               # routes --elevated to the helper, else boots UI
├── MainWindow.xaml(.cs)      # the flat list + action buttons
└── KeyboardManager.config.json  # default reset target (US + Russian)
```

## Key domain concepts

(Defined precisely in `CONTEXT.md` — this is a quick orientation.)

- **Active layout** — actually loaded in the session, visible in Win+Space.
- **Declared layout** — in `HKCU\Keyboard Layout\Preload`, reachable via Settings.
- **Ghost layout** — Active but not Declared; invisible to the Settings "Remove" button. The problem this tool solves.
- **Layout source** — one of four registry locations a layout loads from.
- **Orphan** — a dangling `Substitutes` entry whose source id appears in no Preload key.

## Registry sources the tool reads/writes

| Source | Path | Read | Write |
|---|---|---|---|
| HKCU Preload | `HKCU\Keyboard Layout\Preload` | yes | yes (no elevation) |
| HKCU Substitutes | `HKCU\Keyboard Layout\Substitutes` | yes | yes (no elevation) |
| .DEFAULT Preload | `HKU\.DEFAULT\Keyboard Layout\Preload` | yes | yes (**needs UAC**) |
| .DEFAULT Substitutes | `HKU\.DEFAULT\Keyboard Layout\Substitutes` | yes | yes (**needs UAC**) |
| Layout names | `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\<id>` | yes | never |

## Safety model

Every destructive operation (Remove, Reset) follows three layers:

1. **Automatic `.reg` backup** to `./backups/<timestamp>-<operation>.reg` before any write.
2. **Concrete confirmation** — a dialog listing the exact values and keys that will change.
3. **Honest post-op guidance** — sign-out may be required for `.DEFAULT`-sourced ghosts to clear.

## Configuration

The Reset target set is read from (in order):
1. `KeyboardManager.config.json` next to the exe
2. `%APPDATA%\KeyboardManager\config.json`
3. Built-in default: English (US) + Russian

Edit the JSON to customise — no rebuild required.

## Testing

Tests live in `tests/KeyboardManager.Tests/` and use `FakeKeyboardLayoutRegistry` (an in-memory `IKeyboardLayoutRegistry`) to exercise the inspection, removal, reset, and elevation-helper logic without touching the real registry. `BackupServiceTests` are integration tests that shell out to `reg.exe`.

---

## Agent skills

### Issue tracker

Issues live as local markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the five canonical triage labels as-is. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout — one `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
