# CONTEXT — Keyboard Layout Manager

A glossary of the domain terms used in this project. Implementation-neutral; describes the problem space only.

## Layout

A keyboard **layout** is a specific mapping from physical keys to characters that Windows applies for text input — e.g. `US (QWERTY)`, `Russian`, `US-International`. A layout is distinct from a **language** (English, Russian), though Settings groups them.

## Layout identifier

A Windows registry identifier for a layout, formatted as an 8-digit hex string, e.g. `00000409` (US English), `00000419` (Russian). Extended layouts use a leading `d` region, e.g. `00010409` (US-International). Stored as `REG_SZ` values in the registry.

## Active layout

A layout **actually loaded into the current user session** — the set surfaced by the `GetKeyboardLayoutList` Win32 API and shown in the Win+Space input switcher. This is what the user actually experiences and what can interfere with typing.

## Declared layout

A layout that the Windows Settings UI shows as installed — i.e. the set described by `HKCU\Keyboard Layout\Preload` (and its `Substitutes`). Reachable by the normal Settings "Remove" button.

## Ghost layout

An **Active** layout that is **not Declared** — it appears in the Win+Space switcher but is invisible in Settings, so the normal "Remove" button cannot reach it. Ghost layouts are the core problem this tool exists to solve.

## Layout source

A specific place in the Windows registry from which a layout gets loaded. The relevant sources:

- **`HKCU\Keyboard Layout\Preload`** — layouts loaded at current-user logon. Primary seat of Declared layouts.
- **`HKCU\Keyboard Layout\Substitutes`** — remaps a Preload entry to a different layout ID (e.g. `00000409 → 00010409`). Can reference a layout absent from Preload.
- **`HKU\.DEFAULT\Keyboard Layout\Preload`** — layouts for the logon/welcome screen and other pre-logon surfaces. Often hosts ghost entries that Settings never shows.

## Reset

The act of restoring the input configuration to a known-good minimal set — for this tool's default, **English (US)** and **Russian** — by clearing stray sources and writing back a clean Preload/Substitutes set.

## Registry backup

A saved copy (typically a `.reg` file) of the relevant keyboard-layout registry keys, taken before any destructive operation, so the prior state can be restored if something goes wrong.

## Operation

A user-initiated change to the input configuration — **Remove**, **Reset**, or **Backup**. An operation is a *sequence*: it takes a backup, performs the change, attempts to apply it to the running session, and reports what happened. The word distinguishes a multi-step mutation from a single registry write or a read of the current state.

## Operation result

The data returned by an operation — what was done, whether a backup was taken, whether a sign-out is recommended, what errors occurred, and a human-readable summary. The result is the single contract between the module that performs operations and the UI that renders them, and it is the test surface for the operation flows.

## Orphaned substitute

A **Substitutes** entry whose source id appears in no **Preload** key. Unlike an **Orphan** layout status (a dangling entry shown to the user), an orphaned substitute arises *as a consequence* of removing a layout: deleting a Preload slot can leave its matching Substitutes entry dangling. Removing a layout must also clean up the substitutes that would orphan as a result.

## Resolution

The act of computing, from the four raw registry maps, the complete set of layouts actually loaded in the system — including, for each layout, its sources, its canonical id (with the `d`-prefix stripped), and which substitute entries feed it. Resolution is the single rule that defines how ghost layouts exist: a Preload slot holds an id, and if that id is a key in Substitutes, the loaded layout is the substitute target. This rule lives in exactly one module — the **LayoutResolver** — so that a change to it is a one-file edit.

## Resolved layout

The result of **resolution** — one entry in the resolved snapshot: what is loaded (`LoadedLayoutId`), its canonical form (`CanonicalLayoutId`), its human-readable name, its **status** (Ghost / Declared / Orphan), and the list of registry **sources** that feed it, each carrying the raw id it held and (where applicable) the substitute target it mapped through. The resolved layout is the single type the UI binds to and the operation layer mutates — no projection layer between them.
