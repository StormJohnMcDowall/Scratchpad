# Post-it Restyle & Custom Window Chrome — Design

## Intent

Make Scratchpad look and feel like a pale-yellow Post-it note rather than a
plain, stark Windows utility window — the whole window, not just the
writing surface, so it reads as one object sitting on the desktop rather
than an app with a note inside it. That means replacing the native
Windows title bar too, since a default white title bar would break the
illusion.

## Goals

- The whole `MainWindow` — toolbar, buttons, and writing surface alike —
  uses a warm pale-yellow palette, matching the approved mockup direction
  (full sticky note, not just the text area).
- The native Windows title bar is gone, replaced by the app's own: a
  single control that hides the window to the tray, doing exactly what
  the X button does today. No separate "exit" control in the title bar —
  Exit stays tray-menu-only, unchanged.
- No minimize-to-taskbar control and no maximize control — the window can
  be hidden (tray) or resized, nothing else.
- The window remains draggable (click-and-hold empty toolbar space to
  move it) and remains resizable by dragging an edge — both free with the
  native title bar today, both need rebuilding once it's gone.
- The hide control and the drag area live in the *existing* toolbar row,
  not a new second header strip — keeps the window's current compact
  height instead of growing it.

## Non-goals

- Restyling `SettingsWindow` — it keeps its native title bar and default
  look for this increment. Only `MainWindow` (the pad itself) changes.
- Dark mode / theme switching — a separate roadmap item with its own
  design pass; this is a single fixed palette, not a theme system.
- Maximize, Aero-snap, or any other native window-management affordance
  beyond drag-to-move and edge-resize. If Windows itself still offers
  "Minimize"/"Maximize" via the taskbar icon's right-click menu (a
  shell-level menu outside the app's control), that's acceptable — the
  goal is "no button for it in the app," not "physically impossible by
  any path."

## Anti-goals

None identified beyond the non-goals above.

## Constraints

- No new dependencies — WPF's own `WindowStyle="None"` plus (if needed)
  `System.Windows.Shell.WindowChrome` cover custom chrome; both ship with
  the framework already in use.
- Must not regress existing window-placement persistence
  (`WindowPlacementStore`) or the tray/single-instance/always-on-top
  behavior already in `MainWindow.xaml.cs` — this is a visual and chrome
  change, not a behavioral rewrite of what's already working.

## Context: monitor setup

A builder check flagged `WindowChrome`'s known .NET Framework bugs around
dragging/resizing a window live across monitors with *different* DPI
scaling. Checked with the user: the external display and the laptop's own
screen are never active at the same time — the laptop is always closed
when docked to the external display. So this never hits the worst-case
"drag between two differently-scaled, simultaneously-active monitors"
scenario `WindowChrome` is known to mishandle. The DPI scale can still
differ *between sessions* (e.g. reopening on the laptop's own screen after
undocking, at a different scale than the external display used last) —
that's the scenario `WindowPlacement.Clamp`/`GetScreenBoundsInDeviceIndependentUnits`
already exist to handle, and isn't new risk this feature introduces.

## Decided (by the user)

- Full sticky-note look: the *entire* window (not just the editor/preview
  area) takes the pale-yellow palette shown as "Option A" in the
  brainstorm mockup — toolbar and buttons restyled to match, not left
  neutral.
- Native title bar replaced entirely with a custom one.
- Exactly one custom window-chrome control: hide-to-tray, matching the
  current X button's behavior. No separate exit button in the chrome.
- No minimize-to-taskbar capability in the new chrome.

## Decided (my call, as the builder)

- **Palette**, carried over verbatim from the approved mockup: window/body
  background `#FFF3A3`, toolbar strip background `#FDEE8C`, borders
  `#E6D978`, button fill `#FFE066` with `#D4B942` borders and ~6px rounded
  corners, text color `#5C4F1A`. These are a starting point for the plan,
  not pixel-locked — small adjustments for contrast/readability during
  implementation are expected.
- **No second header row.** The hide button sits at the right-hand end of
  the existing toolbar (after Settings), and empty space in that same row
  is the drag handle — clicking and holding anywhere in the toolbar that
  isn't a button moves the window, the same gesture a native title bar
  gives for free.
- **The window keeps being resizable and draggable** — both are
  unaffected behaviors the user already has today via the native chrome;
  removing that chrome doesn't mean giving these up, it means rebuilding
  them deliberately (`DragMove()` for drag; `WindowChrome` or equivalent
  for edge-resize hit-testing, since a bare `WindowStyle="None"` window
  often loses resize-by-edge-drag without it — a known WPF gotcha the
  plan should research and handle, not assume away). A builder check
  flagged the specific trap to avoid: don't mark the toolbar a native
  caption area via `WindowChrome.CaptionHeight` to get dragging "for
  free" — that silently reintroduces double-click-to-maximize and
  edge-to-edge Aero-snap, both explicitly out of scope. Keep
  `CaptionHeight="0"`, implement drag with a manual `DragMove()` handler
  on the toolbar background, and use `WindowChrome.ResizeBorderThickness`
  for edge-resize on its own.
- **No maximize control**, consistent with dropping minimize — this stays
  a small, single-purpose pad, not a window someone snaps to half the
  screen.
- `ShowAndActivate`'s existing minimized-state handling
  (`MainWindow.xaml.cs`) can stay as defensive code even though there's
  no in-app way to trigger it anymore — Windows' own taskbar-icon
  right-click menu can still minimize a window regardless of its chrome,
  and this code already handles that gracefully.

## What's left to the builder (the plan)

- Exact XAML restructuring of the toolbar row (where the hide button
  goes, how the drag-handle region is implemented without swallowing
  clicks meant for the existing buttons).
- The hide button's exact visual (icon vs. "×" glyph vs. styled text) and
  hover/pressed states.
- Whether `WindowChrome` is actually needed for resize to keep working
  with `WindowStyle="None"`, versus some lighter-weight approach — to be
  determined by testing on the target Windows machine, since this
  sandbox cannot run the WPF shell.
- Precise control-template restyling needed for `ToggleButton`/`Button`
  to show the new palette (WPF's default button chrome doesn't take a
  `Background` color cleanly without a template override in many cases)
  — exact templates are an implementation detail.
- Whether any contrast/accessibility tweaks to the exact hex values are
  needed once seen rendered for real (e.g. button text legibility).
