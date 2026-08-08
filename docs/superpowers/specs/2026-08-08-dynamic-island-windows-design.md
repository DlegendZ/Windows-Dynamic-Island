# Dynamic Island for Windows — Design Spec

Source: `doc/dynamic-island-windows-docs.pdf` (user-supplied PRD + System Design + Animation Spec). This file consolidates that source into repo docs and records the one open decision resolved during brainstorming.

## 1. Product Summary

Windows desktop app that renders real Windows notifications as an iOS-style Dynamic Island: a pill/capsule floating top-center of the screen. Appears, expands, shows notification detail with smooth animation — replacing the perceived experience of standard Windows toast popups.

## 2. Problem

Windows Action Center toasts are rigid, bottom-right, generic animation. No native way to get a modern, playful, unobtrusive notification UX like iOS Dynamic Island.

## 3. Target User

Self / power users comfortable with one manual Windows setting (Focus Assist).

## 4. Fundamental Constraint

Windows has no official API to intercept/hide toast rendering, and no official API to toggle Focus Assist programmatically.

**Resolved approach:** user manually enables Focus Assist (Priority or Alarms only). With Focus Assist on, Windows suppresses toast popups but still delivers notifications silently to Action Center. This app uses `UserNotificationListener` (official WinRT API) to read Action Center in real time and re-renders as a custom Dynamic Island. No registry hacks.

## 5. Scope — MVP (In)

- Notification listening via `UserNotificationListener` (app name, title, body, icon, progress)
- Borderless/transparent/always-on-top window, fixed top-center
- Compact state (pill): icon + short title, auto-appears on new notification
- Expanded state: click pill → full body, app icon, timestamp
- Auto-hide after idle
- Live Activity: progress-bar notifications update live while active in Action Center
- Smooth spring-like animation on all state transitions (see Animation Spec)
- Dismiss: close button or click-outside → removes from Island only (not from Action Center)
- First-run onboarding: instructs user to enable Focus Assist manually
- Focus Assist status check at startup, warning banner if not active

## 6. Scope — Out (Future)

- Real notification actions (Reply, Open App) — click only expands/collapses
- Auto-toggling Focus Assist
- Drag-to-reposition
- Third-party custom notification sources (webhook/API)
- Multi-monitor support
- Theming/customization UI
- Notification history/log

## 7. User Flow

1. First launch → onboarding screen: enable Focus Assist manually (Settings → System → Focus Assist)
2. App runs in background (tray icon)
3. Starts listening via `UserNotificationListener`
4. New notification arrives → real toast suppressed (Focus Assist on) → Island slides+scales in from top as compact pill
5. Pill shows sender icon + short preview for a few seconds
6. Click pill → expand to full detail (title, body, app name, time)
7. Click again / click outside → collapse to compact, then auto-hide after idle
8. Progress notifications (e.g. downloads): pill persists with live-updating progress bar until done, then auto-hides

## 8. Success Criteria

- Real Windows toasts never appear while Focus Assist is active
- New Action Center notifications appear in Island within < 1s
- 60fps animation, no jank on compact↔expanded transitions
- Progress bar updates with no perceptible delay
- Stable background operation, no memory leak over 8+ hour sessions

## 9. Tech Stack

| Layer | Choice | Why |
|---|---|---|
| UI Framework | WPF (.NET 8) | Native, custom-shaped borderless windows, Storyboard/Easing animation |
| Language | C# | Smoothest WinRT interop |
| Notification API | `Windows.UI.Notifications.Management.UserNotificationListener` (WinRT via CsWinRT / `Microsoft.Windows.SDK.Contracts`) | Only official API to read Action Center |
| Window styling | Win32 interop (`WS_EX_TOOLWINDOW`, `WS_EX_TOPMOST`, `WS_EX_LAYERED`) | Borderless, no taskbar icon, always-on-top, transparent |
| System tray | `System.Windows.Forms.NotifyIcon` (WPF interop) | Tray icon for minimize/quit |
| App packaging | **Unpackaged .exe + COM activation** (resolved decision, see §13) | Avoids MSIX/installer/signing overhead for a personal-use app |

## 10. Component Architecture

```
App.xaml.cs (entry point, DI setup, tray icon lifecycle)
   |
   +-- NotificationService (background listener)  --raises NotificationReceived-->  IslandViewModel (MVVM)
   |                                                                                    - CurrentState (Hidden/Compact/Expanded)
   +-- IslandWindow (View: borderless, topmost)    <--binds to--                        - CurrentNotification
                                                                                          - Commands: Expand/Collapse/Dismiss
                                                                                              |
                                                                                          FocusAssistChecker
                                                                                          (read-only registry check)
```

### 10.1 NotificationService
- `UserNotificationListener.Current.RequestAccessAsync()` on first run → triggers native Windows permission dialog (one-time approval)
- Subscribes to `NotificationChanged` for new notifications (event-driven, not polled)
- Live progress: Windows doesn't always fire granular per-update events → poll active progress notifications every ~500ms–1s as fallback
- Parses each `UserNotification`: `AppInfo.DisplayInfo` (name+icon), `Notification.Visual` (toast XML title/body), progress binding if present (`ToastGenericBinding` with `ProgressBar` element)

### 10.2 IslandWindow
- `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `Topmost="True"`
- Window itself is sized to the max footprint (expanded size) and stays static; inner Border/Grid elements animate `Width`/`Height`/`CornerRadius` — native window resize per frame is expensive and janky, so avoid it
- Position computed once at startup from `SystemParameters.PrimaryScreenWidth`, docked top-center with a small top margin
- `WS_EX_TOOLWINDOW` so it's excluded from Alt-Tab and taskbar

### 10.3 IslandViewModel (MVVM)
Three states: `Hidden` (default: fully invisible, no notification active) → `Compact` (pill, auto-shown on new notification) → `Expanded` (full detail, click-triggered). State transitions drive animation triggers.

### 10.4 FocusAssistChecker
- Read-only registry read of Focus Assist status (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount...` — path undocumented, verify against actual dev machine registry at implementation time, may vary by Windows build)
- Never writes to registry
- Best-effort: if read fails, app still runs normally, banner falls back to generic "please verify Focus Assist" message rather than crashing/erroring

## 11. Data Flow — New Notification

```
Windows Notification fires
  -> Action Center receives it silently (Focus Assist ON)
  -> UserNotificationListener.NotificationChanged fires
  -> NotificationService parses toast XML -> Notification model
  -> NotificationReceived event -> IslandViewModel
  -> ViewModel sets CurrentNotification, State = Compact
  -> IslandWindow animates Hidden -> Compact
  -> (if HasProgress) polling loop starts, animates progress value
  -> user clicks -> State = Expanded -> animate Compact -> Expanded
  -> idle timeout / dismiss -> State = Hidden -> exit animation
```

## 12. Data Model

```
Notification
├── Id (uint, Windows notification id)
├── AppName (string)
├── AppIconPath (string / BitmapImage)
├── Title (string)
├── Body (string)
├── HasProgress (bool)
├── ProgressValue (double, 0.0-1.0)
├── ProgressStatusText (string, e.g. "45% - 2.1 MB/s")
└── ReceivedAt (DateTime)
```

## 13. Permission & App Identity (resolved decision)

- App requests notification listener access on first run (native Windows approval dialog)
- **Decision: unpackaged app using classic COM activation** to satisfy WinRT's app-identity requirement, instead of MSIX packaging. Rationale: personal-use tool, avoids installer/signing/packaging overhead; tradeoff is this path has known unpackaged-WPF gotchas (per source doc) that must be validated in the spike below before any UI work starts.
- Auto-start via Task Scheduler or Startup folder shortcut: optional, not required for MVP

## 14. Risks Requiring Spike (validate before full build)

| Risk | Mitigation |
|---|---|
| Unpackaged WPF app + COM activation may hit friction accessing `UserNotificationListener` (usually smoother in packaged MSIX apps) | First build step is a throwaway spike: empty window, call `RequestAccessAsync()`, print notifications to console. Must work before any UI is built. |
| Progress notifications don't always fire granular per-update events | Poll active progress notifications as fallback (already designed above) |
| Focus Assist registry path unstable across Windows builds | Checker is best-effort; on read failure, skip/fallback to generic banner, never crash |
| Multi-monitor: top-center positioning may be wrong on multi-monitor setups | Out of scope for MVP; just ensure no crash — default to primary monitor |

## 15. Non-Functional Requirements

- Idle memory footprint: < 100MB
- Idle CPU: near 0% (event-driven listener, no continuous polling when idle)
- Startup time: < 2s to tray icon appearing

## 16. Animation Spec

Animation is the single most important differentiator between "real Dynamic Island feel" and "generic popup". All values below are fixed — do not improvise timing/easing during implementation.

### 16.1 Principles
- iOS Dynamic Island uses spring physics, not flat easing curves. Characteristics: slight overshoot then settle (not a hard stop at target); transition duration feels fast but not instant (~300–500ms); shape properties (corner radius, width, height) morph **simultaneously**, never sequentially.

### 16.2 WPF Approach (decision: Option A)
WPF has no native spring animation API. Use `BackEase` (EaseOut, Amplitude ≤ 0.4, target 0.2–0.3) for shape/position overshoot, combined with `CubicEase EaseOut` for properties that must not overshoot (e.g. Opacity — overshoot on opacity causes flicker). A physically-accurate custom spring via per-frame `CompositionTarget.Rendering` calculation is explicitly **not** MVP scope — only a future polish option if Option A feels insufficiently smooth.

### 16.3 Per-Transition Spec

**Hidden → Compact** (new notification arrives)
| Property | From | To | Duration | Easing |
|---|---|---|---|---|
| Width | 0 (or 40px round) | ~200px | 350ms | BackEase(0.25) |
| Height | 36px | 36px | - | - |
| CornerRadius | 18 | 18 | - | - |
| Opacity | 0 | 1 | 200ms | CubicEase EaseOut |
| TranslateY | -20px | 0 | 350ms | BackEase(0.2) |

Combined effect: pill emerges from the notch, drops slightly, widens, tiny overshoot settle.

**Compact → Expanded** (user click)
| Property | From | To | Duration | Easing |
|---|---|---|---|---|
| Width | ~200px | ~380px | 400ms | BackEase(0.2) |
| Height | 36px | ~120px | 400ms | BackEase(0.2) |
| CornerRadius | 18 | 24 | 400ms | CubicEase EaseOut (no overshoot — avoids "wobbly" radius) |
| Opacity: compact content | 1 | 0 | 150ms | Linear (fades out first) |
| Opacity: expanded content | 0 | 1 | 250ms, 100ms delay | CubicEase EaseIn |

Width/Height/CornerRadius animate in parallel (same Storyboard), never sequential. Text content cross-fades — never an instant content swap mid-morph.

**Expanded → Compact** (collapse)
Same properties as 16.3 reversed, but: 300ms duration (snappier than expand), `CubicEase EaseInOut`, **no overshoot** — overshoot only happens once, on expand.

**Compact → Hidden** (auto-hide after idle)
| Property | From | To | Duration | Easing |
|---|---|---|---|---|
| Opacity | 1 | 0 | 250ms | CubicEase EaseIn |
| Width | ~200px | 0 | 300ms | CubicEase EaseIn (no overshoot) |
| TranslateY | 0 | -10px | 300ms | CubicEase EaseIn |

**Progress Bar Update (Live Activity)**
| Property | Duration | Easing |
|---|---|---|
| `ProgressBar.Value` (`DoubleAnimation`) | 400ms | CubicEase EaseOut |

Never snap directly to a new polled value — always animate, or it looks "patchy" under 500ms polling.

### 16.4 Global Timing
- Compact auto-hides after **4s** idle (suppressed while a progress notification is active — those persist until done or manually dismissed)
- Expanded has no independent auto-hide timer — collapses to Compact after **6s** idle, then the normal 4s compact idle timer applies

### 16.5 Anti-patterns (do not do these)
- No `LinearEase` on shape transitions (width/height/corner radius) — reads as robotic
- No overshoot on Opacity — causes flicker
- Never animate Width and Height with different durations in the same morph — causes a transient "wrong aspect ratio" look
- Never hard-swap text content without cross-fade during compact↔expanded — looks like two different UIs being swapped

### 16.6 Cheat Sheet
```
Compact width:        ~200px       Expand duration:    400ms, BackEase(0.2)
Compact height:       36px         Collapse duration:  300ms, CubicEase EaseInOut
Expanded width:       ~380px       Exit duration:      300ms, CubicEase EaseIn
Expanded height:      ~120px       Progress bar anim:  400ms, CubicEase EaseOut
Corner radius compact:   18        Auto-hide idle:     4s (compact), 6s (expanded)
Corner radius expanded: 24
Enter duration:      350ms, BackEase(0.25)
```

## 17. Suggested Build Order (from source doc, unchanged)

1. Spike/validate: empty WPF project, call `UserNotificationListener.RequestAccessAsync()` from unpackaged app, confirm it works before any UI (§14)
2. Build basic `IslandWindow`: borderless, transparent, topmost, top-center — static, no animation
3. Implement `NotificationService`, confirm real notification data parses into `Notification` model
4. Wire to `IslandViewModel`, get Hidden/Compact/Expanded working (plain show/hide, no animation yet)
5. Add animation per §16 — last, because it needs a stable state machine first
6. Add `FocusAssistChecker` + onboarding/warning banner
7. Add live progress polling for progress-bar notifications
