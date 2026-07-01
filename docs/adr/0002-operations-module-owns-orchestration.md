# ADR-0002 — An operations module owns the orchestration; the window only renders

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

The first implementation of this tool placed the Remove, Reset, and Backup
multi-step flows directly inside `MainWindow.xaml.cs`. Each flow interleaved
backup, confirmation dialogs, execution, session apply, and user reporting —
roughly 80 lines per operation, with `MessageBox.Show` calls scattered between
logic steps and `IsBusy` toggles hand-repeated across four methods.

The individual services (`LayoutRemovalService`, `LayoutResetService`,
`BackupService`, `SessionLayoutApplier`) are deep and well-unit-tested against
an in-memory fake registry — 21 tests pass. But the *flows* that sequence those
services — the part most likely to regress (a missed backup, a dropped error
path, a wrong sign-out note) — are the one part a maintainer cannot reach from a
test, because they live behind modal dialogs and a UI thread.

An architecture review (`improve-codebase-architecture` skill, 2026-07-01)
identified this as the top friction site.

## Decision

Introduce a **`LayoutOperations`** module that owns the three operation flows
(Remove, Reset, Backup). It exposes a narrow interface:

```csharp
public OperationResult Remove(LayoutEntry entry);
public OperationResult Reset(KeyboardManagerConfig config);
public OperationResult Backup();
```

Each method performs its full sequence — backup → execute → best-effort apply →
build result — and returns a single `OperationResult`. The module never calls
`MessageBox`, never touches UI state (`IsBusy`), and never asks for
confirmation; it is deterministic given its inputs. Confirmation dialogs stay
in the window, which decides whether to call the module at all.

The window (and `MainViewModel`) shrinks to: hold state, render the layout
list, ask for confirmation, call an operation, render the result. A small
`RunOperation` helper in the window centralises the `IsBusy` toggle so it is
not repeated per button.

`OperationResult` is one DTO shared across all three operations:

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

The result is both the test surface (assert against it) and the UI contract
(render from it).

## Consequences

- **The flows become the test surface.** A test constructs `LayoutOperations`
  with a fake registry, fake applier, and fake elevation runner, calls
  `Remove(entry)`, and asserts on `OperationResult` — no UI thread, no modal
  dialog, no P/Invoke.
- **The window shrinks dramatically.** The ~80-line `OnRemove` collapses to a
  confirmation call plus `RunOperation(() => operations.Remove(entry))`.
- **Two new interfaces emerge** as side effects: `ISessionLayoutApplier` (so
  the best-effort apply can be faked) and `IElevatedOperationRunner` (so the
  `.DEFAULT` write path can be faked). These resolve the "two adapters = real
  seam" threshold for both collaborators and align with Candidates 3 and 4 of
  the architecture review.
- **`KeyboardManagerConfig` stays out of the constructor** — it is passed as a
  parameter to `Reset`, keeping the module free of disk I/O at construction
  time.
- **`LayoutInspector` stays in `MainViewModel`** — the operations module
  mutates state; it does not read it. Refresh is a read, so it stays in the VM.

## Alternatives considered

- **Commands in `MainViewModel` (Option B in the design discussion).** More
  WPF-idiomatic, but the ViewModel would own both state and orchestration,
  becoming a god class whose tests would have to fake UI confirmation callbacks
  — re-introducing the very coupling the extraction was meant to remove.
- **A hybrid: operations module + VM commands delegating to it.** Adds a layer
  with no additional leverage over the pure module; the window can call the
  module directly.
- **An `IUserConfirm` callback injected into the operations module.** Would let
  the module own the full flow including prompts, but injects a UI abstraction
  into a domain module and forces tests to fake confirmation. Rejected: the
  module should return results, not produce side effects.
