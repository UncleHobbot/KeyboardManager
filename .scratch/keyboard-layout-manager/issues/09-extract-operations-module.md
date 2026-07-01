# 09 — Extract LayoutOperations module from MainWindow

Status: ready-for-agent

## What

Extract the Remove / Reset / Backup orchestration flows out of `MainWindow.xaml.cs`
into a new `LayoutOperations` module, per ADR-0002. The window keeps only
confirmation prompts and result rendering; the module owns the deterministic
backup → execute → apply → result sequence.

## Why

The services are tested; the *flows* that sequence them are not — they live
behind modal dialogs and a UI thread (architecture review 2026-07-01, top
candidate). Making the flows the test surface is the goal.

## Steps (do in order)

### 9.1 — Introduce `ISessionLayoutApplier`
- Extract interface from `SessionLayoutApplier` (`TryUnload`, `BroadcastSettingsChange`).
- `SessionLayoutApplier` implements it.
- Update `LayoutResetService` constructor to take the interface.

### 9.2 — Introduce `IElevatedOperationRunner`
- Extract interface (one method: `Run(IReadOnlyList<ElevatedOperation>) → ElevatedResult`).
- `ElevatedOperationRunner` implements it.
- Update `LayoutRemovalService` constructor to take the interface.

### 9.3 — Add `OperationResult` model
```csharp
public sealed record OperationResult(
    bool Success,
    string Summary,
    string? BackupPath,
    bool NeedsSignOut,
    int? ValuesChanged,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Notes);
```

### 9.4 — Add `LayoutOperations` service
Constructor takes `BackupService`, `LayoutRemovalService`, `LayoutResetService`,
`ISessionLayoutApplier`. Three methods:
- `OperationResult Remove(LayoutEntry entry)` — backup, `removal.PlanRemoval` +
  `Execute`, `applier.TryUnload`, build result (`NeedsSignOut` from
  `plan.NeedsElevation`).
- `OperationResult Reset(KeyboardManagerConfig config)` — backup,
  `reset.Reset`, `applier.BroadcastSettingsChange`, build result (summary from
  `LayoutResetService.DescribeTarget`).
- `OperationResult Backup()` — `backup.BackupAll("manual")`, build result.

### 9.5 — Slim `MainWindow.xaml.cs`
- Construct `LayoutOperations` once.
- Replace `OnRemove` body with: confirm (using `removal.PlanRemoval` for the
  target list), then `RunOperation(() => operations.Remove(entry))`.
- Same for `OnReset`, `OnBackupNow`.
- Add `RunOperation(Func<OperationResult>)` helper: toggles `IsBusy`, calls the
  func, renders the result (summary → status bar, errors → MessageBox), returns.
- `MessageBox.Show` appears only in `RunOperation`'s result renderer and the
  confirmation helpers — never inside the operation itself.

### 9.6 — Tests for `LayoutOperations`
Using the existing `FakeKeyboardLayoutRegistry` plus new fakes for applier and
runner:
- Remove of a Ghost from `.DEFAULT`: fake runner returns success → result has
  `NeedsSignOut = true`, `Success = true`.
- Remove when backup is skipped (fake backup that throws) → `Success = false`,
  errors populated.
- Reset builds expected summary with target description.
- Backup returns `BackupPath` set, `ValuesChanged` null.
- UAC declined: fake runner returns "declined" error → result propagates it.

## Acceptance

- `LayoutOperations` has no reference to `MessageBox`, `Window`, or any WPF UI type.
- `MainWindow.xaml.cs` contains no backup/execute/apply orchestration — only
  confirm + `RunOperation`.
- New tests cover the three flows, including the `.DEFAULT`/UAC branch, without
  a UI thread.
- All existing tests still pass.
