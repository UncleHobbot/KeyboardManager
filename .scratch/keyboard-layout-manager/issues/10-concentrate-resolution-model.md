# 10 — Concentrate the resolution model into LayoutResolver

Status: ready-for-agent

## What

Replace the five smeared copies of the substitute-resolution rule + `d`-prefix
canonicalisation with a single `LayoutResolver` module that returns a rich,
immutable snapshot. `LayoutInspector` and `LayoutEntry` are deleted;
`ResolvedLayout` replaces them everywhere. Per ADR-0003.

## Why

The resolution rule is the most likely thing in the domain to evolve, and today
a maintainer fixing it must edit five sites (architecture review 2026-07-01,
Candidate 2). Concentrating it in one module is the locality fix.

## Steps (do in order)

### 10.1 — Add the resolved-snapshot models

New files under `Models/`:

```csharp
public sealed record ResolvedLayoutSet(IReadOnlyList<ResolvedLayout> Layouts);

public sealed record ResolvedLayout(
    string LoadedLayoutId,
    string CanonicalLayoutId,
    string DisplayName,
    LayoutStatus Status,
    IReadOnlyList<ResolvedSource> Sources);

public sealed record ResolvedSource(
    LayoutSourceKind Kind,
    string SlotName,
    string RawLayoutId,
    string LoadedLayoutId,
    string? ViaSubstitute);
```

### 10.2 — Add `LayoutResolver`

New `Services/LayoutResolver.cs`. Constructor takes `IKeyboardLayoutRegistry`.
Single method `Resolve() → ResolvedLayoutSet` that:

- Reads the four registry maps once.
- Resolves substitutes (forward: slot → loaded id).
- Detects existing orphans (substitute whose source id is in no Preload).
- Classifies status (Ghost / Declared / Orphan).
- Canonicalises the `d`-prefix (the single copy in the codebase).
- Builds display names from HKLM.
- Populates `RawLayoutId` and `ViaSubstitute` on each source.

This is a port of `LayoutInspector`'s logic, enriched with the reverse fields
and the canonicalisation that currently lives in `LayoutEntry`/`Applier`.

### 10.3 — Delete `LayoutInspector` and `LayoutEntry`

- Delete `Services/LayoutInspector.cs`.
- Delete `Models/LayoutEntry.cs`.
- `LayoutSourceEntry` is replaced by `ResolvedSource` — delete it too.

### 10.4 — Rewrite `LayoutRemovalService` against the snapshot

- `PlanRemoval(ResolvedLayout entry)` — no longer re-reads the registry. Computes
  local vs elevated deletes from `entry.Sources` directly. Orphan cleanup: for
  each removed Preload source, look up its `RawLayoutId` and schedule deletion of
  any substitute source in the same snapshot whose `SlotName` equals that raw id.
  All pure projection over the snapshot.
- `Execute` is unchanged (still calls registry + elevation runner).
- `RemovalTarget`, `RemovalPlan`, `RemovalResult` records stay (their `Kind` field
  now keys off `LayoutSourceKind` as before).

### 10.5 — Slim `SessionLayoutApplier`

- Remove the `d`-prefix canonicalisation from `TryUnload`. It now expects an
  already-canonical id (the caller passes `ResolvedLayout.CanonicalLayoutId`).

### 10.6 — Update `LayoutOperations`

- `Remove(ResolvedLayout entry)` instead of `Remove(LayoutEntry)`.
- The `LayoutOperations` constructor takes `LayoutResolver` instead of being
  constructed alongside an inspector. (Or the VM holds the resolver for refresh
  and passes resolved layouts into operations — decide at implementation time.)

### 10.7 — Update `MainViewModel` and `MainWindow`

- `MainViewModel` holds `LayoutResolver` and an `ObservableCollection<ResolvedLayout>`.
- `Refresh()` calls `resolver.Resolve()` and repopulates.
- `SelectedEntry` becomes `ResolvedLayout?`.
- `MainWindow` confirm/removal/render paths use `ResolvedLayout`.
- Converters bind to `ResolvedLayout.Status`, `.DisplayName`, `.Sources` — the
  `SourcesToTextConverter` reads `SlotName` (renamed from `ValueName`).

### 10.8 — Port tests

- `LayoutInspectorTests` → `LayoutResolverTests`. Same scenarios (developer-machine
  ghost, orphan, merge-across-hives, sort order, unknown-id fallback), now asserting
  on `ResolvedLayout` and checking `RawLayoutId` / `ViaSubstitute` where relevant.
- `LayoutRemovalServiceTests` — update to construct `ResolvedLayout` inputs instead
  of `LayoutEntry`; assert plans no longer require the fake registry to be re-read.
- `LayoutOperationsTests` — update `MakeEntry` helper to build `ResolvedLayout`.
- All other tests (backup, reset, config, elevation helper) unchanged or trivially
  updated for the rename.

## Acceptance

- No file other than `LayoutResolver` contains the substitute-resolution rule or
  the `d`-prefix transform.
- `LayoutRemovalService.PlanRemoval` does not call any `IKeyboardLayoutRegistry`
  read method — it is a pure projection over the snapshot.
- `LayoutInspector`, `LayoutEntry`, and `LayoutSourceEntry` no longer exist.
- All tests pass (ported to the new types), including the developer-machine ghost
  scenario.
- App launches and renders layouts against the live registry.
