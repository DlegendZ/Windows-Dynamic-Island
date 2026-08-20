# Dynamic Island for Windows

A borderless, transparent, always-on-top pill widget for Windows 10/11, inspired by Apple's Dynamic Island.

## Status

Core shell only (window states, animations, module hosting contract, tray, autostart). Feature modules — media controls, focus timer, notifications, system stats, brightness/battery/volume, AI assistant, tasks/notes/agenda — are specced and built separately; see `docs/superpowers/specs/`.

**Manual verification outstanding.** Automated tests (below) pass, but the following spec §13 checklist items require a human with a real Windows desktop and have NOT been performed in this build environment (no GUI/desktop access here — only CLI and browser automation):

- Borderless/transparent/topmost/no-taskbar-entry (visual confirmation)
- Click-through in Idle, interactive in Peek/Expanded
- Keyboard-only pass reaching every control (Tab, Arrows, Enter, Esc)
- Narrator announcing a sensible name for every focusable element
- Idle CPU/RAM sampled over 10+ minutes (Task Manager) staying near-zero CPU / flat RAM
- Windows 10 compatibility — untested entirely; no Windows 10 machine was available. Behavior there is unknown, not just unverified.

Do not treat this as a finished, working app until someone with desktop access has run through spec §13 on both Windows 11 and Windows 10.

## Build and run

```bash
dotnet build
dotnet run --project src/DynamicIsland
```

## Test

```bash
dotnet test
```

## Project layout

- `src/DynamicIsland.Core` — state machine, peek event queue, settings, autostart logic (no WPF dependency, fully unit tested)
- `src/DynamicIsland` — WPF window, Win32 interop, tray icon, COM shortcut writer
- `tests/DynamicIsland.Core.Tests` — xunit tests for Core
