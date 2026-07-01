# ADR-0003 — A single LayoutResolver owns the resolution model

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

The rule that defines how ghost layouts exist — *"a Preload slot holds an id; if
that id is a key in Substitutes, the loaded layout is the substitute target"* —
was smeared across five sites:

1. `LayoutInspector.AddPreloadSources` — forward resolution (slot → loaded id).
2. `LayoutRemovalService.PlanRemoval` — re-reads the registry hive (because the
   `LayoutEntry.Sources` it received weren't rich enough to compute orphan
   cleanup).
3. `LayoutRemovalService.AddOrphanedSubstitutes` — reverse resolution (removed
   slot → raw id → substitute cleanup).
4. `LayoutEntry.CanonicalId`, `LayoutInspector.BuildDisplayName`, and
   `SessionLayoutApplier.TryUnload` — three independent copies of the
   `d`-prefix canonicalisation transform.
5. Two different "orphan" predicates lived in two classes with no shared helper:
   `LayoutInspector` finds existing dangling substitutes (shown to the user),
   `LayoutRemovalService` finds substitutes that *would* dangle *after* a removal.

An architecture review (`improve-codebase-architecture` skill, 2026-07-01)
identified this as Candidate 2: the resolution rule is the most likely thing in
the domain to evolve, and a maintainer fixing it had to find and edit five
places.

## Decision

Introduce a **`LayoutResolver`** module that owns the resolution model. It reads
the four raw registry maps once and returns a single immutable snapshot:

```csharp
public ResolvedLayoutSet Resolve();
```

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

The snapshot is rich enough that both consumers become pure readers:

- **The UI** (`MainViewModel`) binds directly to `ResolvedLayout` — no
  projection step.
- **`LayoutRemovalService.PlanRemoval`** computes the removal plan and orphan
  cleanup as a pure projection over the snapshot's `ResolvedSource.RawLayoutId`
  field, **without re-reading the registry**.

`LayoutResolver` also owns:

- The `d`-prefix canonicalisation (the only copy in the codebase).
- Display-name assembly from HKLM `Layout Text` and the LCID-derived language
  name.

### What is removed

- **`LayoutInspector`** is deleted. It had become a thin wrapper once the
  resolver took over resolution + naming + status.
- **`LayoutEntry`** is deleted. `ResolvedLayout` replaces it everywhere (UI,
  operations, removal, tests).
- The `d`-prefix canonicalisation in `SessionLayoutApplier.TryUnload` is removed;
  the applier now receives an already-canonical id and trusts its caller.

## Consequences

- **The resolution rule lives in exactly one module.** A future fix to substitute
  resolution, orphan detection, or canonicalisation is a one-file change.
- **Removal no longer re-reads the registry to plan.** `LayoutRemovalService`
  consumes the immutable snapshot; its plan is a pure function of the snapshot +
  the selected layout. This removes the duplicated read and the unstated contract
  between inspector and removal about what `Sources` guarantees.
- **One type, not two.** `ResolvedLayout` serves both the UI and the operation
  layer — no projection layer, no near-duplicate DTO.
- **The applier is stricter.** `TryUnload` expects a canonical id; passing a
  non-canonical id is a caller bug, not silently papered over.
- **`LayoutOperations.Remove` accepts `ResolvedLayout`**, carrying its rich
  sources into the operation flow.

## Alternatives considered

- **Tiny pure functions** (`ResolveLoadedId`, `FindOrphanedSubstitutes`) instead
  of a snapshot. Rejected: the rule would still be assembled at each call site,
  so locality is not improved — only the helpers move.
- **Keep `LayoutInspector` as a projection layer** (`ResolvedLayout` →
  `LayoutEntry`). Rejected: fails the deletion test — deleting the projection
  concentrates no complexity, it just removes an empty wrapper.
- **Leave `d`-prefix canonicalisation defensive in the applier.** Rejected: it
  duplicates the rule and masks caller bugs.
