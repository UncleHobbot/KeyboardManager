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
