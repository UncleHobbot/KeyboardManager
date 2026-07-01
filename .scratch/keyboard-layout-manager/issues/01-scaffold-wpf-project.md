# 01 — Scaffold WPF .NET 8 project

Status: ready-for-agent

## What

Create the .NET 8 WPF project structure for `KeyboardManager`:

- `src/KeyboardManager/KeyboardManager.csproj` — WPF, `net8.0-windows`, `UseWPF`.
- `App.xaml` / `App.xaml.cs` entry point.
- `MainWindow.xaml` / `MainWindow.xaml.cs` shell.
- Solution file `KeyboardManager.sln` at repo root.

## Why

FR-1..FR-6 all depend on a WPF app shell existing. This is the foundation every
later issue builds on.

## Acceptance

- `dotnet build` succeeds from repo root.
- `dotnet run` opens an empty main window.
- No elevation in the default manifest (per ADR-0001).
