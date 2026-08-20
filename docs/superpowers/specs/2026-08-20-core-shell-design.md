# Dynamic Island for Windows — Core Shell Design (Module 1 of 7)

## 1. Context

This project builds a Windows Dynamic-Island-style pill widget with many feature modules: media controls, focus timer, live notifications, system stats, brightness, battery, volume (incl. per-app), a private AI assistant, and tasks/notes/agenda.

That is 7+ largely independent subsystems glued to one shared shell. Per project scoping, each subsystem gets its own spec → plan → implementation cycle. This document specs **only the core shell** — the window, states, animation, module-hosting contract, and lifecycle that every other module plugs into. Feature modules are out of scope here and will each get their own spec later, starting with Media Controls (Module 2) next.

## 2. Goals

- Slim pill at top-center of the primary screen, invisible/click-through until needed
- Always-on-top, borderless, transparent, works on Windows 10 and 11
- Smooth, modern animations (60fps target)
- Full keyboard navigation and screen-reader (UIA) support
- Low resource use while idle (no polling, no rendering when idle)
- A stable extension point (`IIslandModule`) so feature modules can be added without touching shell code

## 3. Non-Goals (this module)

- Any specific feature module's functionality (media, stats, notifications, timer, tasks, AI) — each is a separate spec
- Multi-monitor support (primary monitor only for now)
- Packaging/installer/code signing (unpackaged .exe)
- Theming/customization UI

## 4. Tech Stack

| Layer | Choice | Why |
|---|---|---|
| UI Framework | WPF, .NET 8 (or later LTS), C# | Native Win32, built-in UIA screen-reader support, cheapest borderless/transparent/topmost window, easy Win32/WMI/WASAPI interop for later modules |
| Window styling | Win32 interop: `WS_EX_TOOLWINDOW`, `WS_EX_TOPMOST`, `WS_EX_LAYERED`, toggled `WS_EX_TRANSPARENT` | Excluded from Alt-Tab/taskbar, always-on-top, transparent, click-through while idle |
| System tray | `System.Windows.Forms.NotifyIcon` (WPF interop) | Tray icon for Show/Settings/Autostart/Quit |
| Packaging | Unpackaged .exe, no installer | Free personal tool, avoids MSIX/signing overhead |

## 5. Window Architecture

One WPF window, sized to the maximum expanded footprint, created once at startup and never resized natively (native resize per frame is expensive/janky). Transparent background. All visible chrome — the pill, the expanded panel — is drawn by inner `Border`/`Grid` elements that animate `Width`, `Height`, and `CornerRadius`/`Margin` via `Storyboard`s, GPU-composited where possible.

Window flags:
- `WS_EX_TOOLWINDOW` — no taskbar entry, no Alt-Tab entry
- `WS_EX_TOPMOST` — always on top
- `WS_EX_LAYERED` — enables true per-pixel transparency
- `WS_EX_TRANSPARENT` — toggled on while in `Idle` state so clicks pass through to whatever is beneath the sliver; toggled off in `Peek`/`Expanded` so the pill is interactive

Position: computed at startup from the primary monitor's work area (top-center, small top margin). Recomputed on `SystemParameters.WorkArea` change (e.g. taskbar move, resolution/DPI change, monitor reconfiguration) so it stays centered on whichever monitor is primary at the time — no live migration if the primary monitor changes mid-session; that requires an app restart.

## 6. States

```
Idle -> Peek -> Expanded
 ^        |         |
 |        v         v
 +------ (auto-hide / click-outside / Esc) -----+
```

- **Idle** — pill collapsed to a thin sliver (or fully invisible, TBD in implementation polish), click-through, no interactivity, nothing is rendered/updated (no timers running).
- **Peek** — compact pill, shows an icon + short text. Entered by: (a) mouse hover near the top-center hotspot, or (b) a module raising a `PeekEvent`. Interactive (click-through disabled). Auto-returns to Idle after a short idle timeout or mouse-leave (whichever applies), unless the user clicks to expand.
- **Expanded** — full dashboard panel: a row of module icons (tabs) across the top, selected module's `Content` below. Entered by clicking the Peek pill. Exits on click-outside, Esc key, or an explicit close control, animating back to Idle.

All transitions are spring/ease `Storyboard` animations, target 60fps. No layout-thrashing properties (e.g. avoid animating `Margin` on deeply nested layouts where cheaper alternatives like `RenderTransform`/`ScaleTransform` work).

## 7. Auto-Peek Event Pipeline

Any module can request the shell show a transient Peek without the user hovering — e.g. media track change, notification arrival, timer completion (those modules are specced separately; this defines the mechanism they'll use).

```csharp
public record PeekEvent(string IconKey, string Text, TimeSpan Duration);

public interface IPeekEventSource
{
    event Action<PeekEvent> PeekRequested;
}
```

Shell subscribes to each loaded module's `PeekRequested` (if it implements `IPeekEventSource`), queues events (simple FIFO, one at a time), transitions `Idle -> Peek`, shows icon+text for `Duration`, then returns to `Idle` (or stays in `Peek`/`Expanded` if the user is actively interacting — auto-peek never interrupts an open Expanded panel).

Module 1 ships this pipeline but nothing calls it yet (no feature modules exist). It will be exercised for real once Module 2 (Media Controls) lands.

## 8. Module Hosting Contract

```csharp
public interface IIslandModule
{
    string Id { get; }
    string Header { get; }              // tab label
    string IconKey { get; }             // tab icon
    UserControl CreateContent();        // lazily created on first tab select
}
```

Expanded panel is a horizontal strip of module icons (one per registered `IIslandModule`), acting like a `TabControl`: `Left`/`Right` arrow keys move focus between tabs, `Enter`/`Space` activates, each tab and its content carry `AutomationProperties.Name` for screen readers. Modules are registered in a simple ordered list at startup (hardcoded order for now — no drag-to-reorder UI in this module).

Feature modules (Media, Stats, Notifications, Timer, Tasks/Notes/Agenda, AI Assistant) will each implement `IIslandModule` (and `IPeekEventSource` where relevant) in their own future specs. This module ships the contract and an empty module list (or at most a placeholder "Coming soon" module for manual testing).

## 9. Input & Accessibility

- Full keyboard navigation: Tab cycles focusable elements, Arrow keys move between tabs in Expanded, Esc collapses Expanded → Idle, Enter/Space activates focused control.
- Every interactive element sets `AutomationProperties.Name`/`HelpText` so Narrator and other screen readers announce it correctly.
- Global hotkey to force show/hide the pill: registered via `RegisterHotKey`, configurable in Settings, unset (disabled) by default.
- Focus handling: opening Expanded moves keyboard focus into the panel; closing returns focus to whatever had it before (standard popup-focus behavior), so keyboard/screen-reader users aren't stranded.

## 10. Idle Resource Use

- No polling of any kind while in `Idle` state.
- No `DispatcherTimer` running unless a Peek auto-hide countdown or an active module (e.g. a running timer) needs one.
- Window paints nothing (fully transparent, minimal visual tree) while idle — no continuous GPU/CPU work.
- Auto-hide timers are created on entering `Peek`/`Expanded` and disposed on leaving them, not left running in the background.

## 11. Lifecycle

- App starts to system tray (`NotifyIcon`), no window flash on launch.
- Tray context menu: **Show/Hide Island**, **Settings**, **Launch at startup** (checkbox, off by default), **Quit**.
- Autostart implemented via a shortcut in the user's Startup folder (`shell:startup`), added/removed when the tray checkbox is toggled — no registry Run-key, no admin rights required.
- Settings persisted to a simple local JSON file (`%LocalAppData%\DynamicIsland\settings.json`): autostart flag, hotkey binding, module order (once reordering exists — not in this module).

## 12. Data Flow — State Transition Example (generic, no real module yet)

```
Mouse enters top-center hotspot (or a module raises PeekEvent)
  -> Shell: Idle -> Peek
  -> Storyboard animates sliver -> compact pill
  -> Peek shows icon+text (from PeekEvent) or a default idle glance
  -> [user clicks pill] -> Shell: Peek -> Expanded
      -> Storyboard animates pill -> dashboard panel
      -> Focus moves to selected tab's content
  -> [Esc / click-outside] -> Shell: Expanded -> Idle
      -> Storyboard animates panel -> sliver, focus restored
```

## 13. Testing

- Manual: verify borderless/transparent/topmost/no-taskbar-entry on both Windows 10 and Windows 11.
- Manual: verify click-through in Idle (clicks reach the app beneath the sliver) and interactive in Peek/Expanded.
- Manual: keyboard-only pass (Tab, Arrows, Enter, Esc) reaches and operates every control.
- Manual: Narrator pass — every focusable element announces a sensible name.
- Manual: idle CPU/RAM sampled over 10+ minutes with nothing happening — should sit near zero CPU, flat RAM (no leak).
- Unit tests where feasible: state-machine transitions (`Idle/Peek/Expanded`) and the `PeekEvent` queue, decoupled from actual WPF rendering.

## 14. Risks

| Risk | Mitigation |
|---|---|
| `WS_EX_TRANSPARENT` toggling has known quirks with hit-testing in WPF | Spike this first: empty sliver window, verify click-through works reliably before building state machine on top |
| DPI/multi-monitor edge cases (primary monitor changes, DPI scaling) | Out of scope beyond "recompute position on WorkArea change"; document as known limitation |
| Screen-reader support for a fully custom, non-standard-controls UI | Use standard WPF controls (Button, ListBox-as-tabs) wherever possible instead of fully custom-drawn controls, so UIA support comes largely for free |
