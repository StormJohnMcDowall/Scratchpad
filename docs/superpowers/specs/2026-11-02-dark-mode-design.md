# Dark Mode — Design

## Intent

When Windows switches to dark — by hand or on its automatic schedule —
Scratchpad stays bright pale-yellow and glares. It should follow Windows
into dark without the user doing anything, while still looking like the
Post-it note it is: a dark Post-it, not a generic dark app. A setting lets
the user override Windows and force light or dark.

## Goals

- **Follow Windows by default.** Scratchpad's theme tracks the Windows
  *apps* light/dark setting (Personalisation → Colours → "Choose your
  default app mode"), not the separate "Windows mode" (taskbar/Start).
- **Switch live.** When the Windows setting changes while Scratchpad is
  running — window visible, or hidden in the tray — Scratchpad re-themes
  immediately. No restart, no need to reopen the window.
- **Dark palette = mockup option A, "Dark Post-it"** (user's choice from
  three mockups): deep olive-brown paper with pale-yellow ink. Reference
  values from the approved mockup, mapped onto today's light brush keys:

  | Brush (existing key)        | Light (today) | Dark (option A) |
  |-----------------------------|---------------|-----------------|
  | `PostItBackgroundBrush`     | `#FFF3A3`     | `#3A3420`       |
  | `PostItToolbarBrush`        | `#FDEE8C`     | `#2F2A19`       |
  | `PostItBorderBrush`         | `#E6D978`     | `#5A5130`       |
  | `PostItButtonBrush`         | `#FFE066`     | `#4A4228`       |
  | `PostItButtonHoverBrush`    | `#FFD633`     | builder's call — between button and pressed |
  | `PostItButtonPressedBrush`  | `#F0C419`     | `#8A7220`       |
  | `PostItTextBrush`           | `#5C4F1A`     | `#F3E6A6`       |

  Pressed/checked button text in dark may need a lighter tone (mockup used
  `#FFF3C4`) — builder's call. The light palette is unchanged.
- **Everything themes together:** main window (toolbar, buttons, editor —
  including caret and text-selection colours so they're visible on dark),
  the rendered preview (body text, headings, inline code, code blocks,
  links, block quotes, tables, task-list checkboxes), the vertical
  scrollbars on the editor and preview (track, thumb and arrows — they
  appear whenever a note is longer than the window), the editor's
  right-click Cut/Copy/Paste menu (user's explicit choice — it is a WPF
  menu, so it must be themed, not left to Windows), and the Settings
  window (its background, labels, combo boxes and their drop-down lists,
  buttons, and — where Windows supports it — its native title bar).
- **Theme override in Settings:** a new choice with three values —
  **Follow Windows** (default), **Light**, **Dark**. Light/Dark ignore the
  Windows setting entirely. Changing it re-themes immediately. It is
  persisted like the other settings (a JSON file under
  `%LOCALAPPDATA%\Scratchpad\`, same best-effort load/save pattern as
  `FontFamilyStore`/`FontSizeStore`; unreadable/missing → Follow Windows).
- **Reset to defaults includes the theme:** resetting sets the theme back
  to Follow Windows (and the Settings picker shows it). The confirmation
  text changes to **"Reset all settings to their defaults?"**; still
  doesn't touch window placement, always-on-top, preview mode, or text.

## Non-goals

- The Yes/No confirmation `MessageBox` and the tray icon's context menu
  (both native Windows UI, not WPF) may stay in Windows' standard look.
  This exception does **not** cover the editor's right-click menu, which
  is in scope above.
- No user-customisable colours, no extra themes beyond light and dark.
- No dark tray icon variant.
- Following the Windows *accent colour* or high-contrast themes.

## Anti-goals

- Dark mode must not look like a generic grey dark app — it is the
  Post-it at night (the user explicitly picked A over neutral charcoal).
- Any surface left bright white/yellow in dark mode (a drop-down list, the
  preview's code-block background, the Settings window) — the whole point
  is to stop the glare.
- Light mode must look exactly as it does today.

## Constraints

- WPF on .NET Framework 4.8, no new NuGet dependencies, no admin rights
  (see the existing project spec). Reading the per-user registry
  (`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`,
  `AppsUseLightTheme`, DWORD: `0` = dark) is fine; if the value is
  missing or unreadable, treat Windows as light.
- The Shell can only be compiled in the dev sandbox, never run — so the
  "which theme should be active" decision (preference + Windows setting →
  light/dark) and the theme persistence live in `Scratchpad.Core` with
  xUnit tests, following the `IRunKeyRegistry` pattern for anything that
  touches the registry.
- Known WPF pitfall from the post-it-restyle feature: a `StaticResource`
  on a root element's own start tag that points into its own
  `.Resources` throws at launch. Runtime re-theming will also need
  brushes referenced as `DynamicResource` (a `StaticResource` won't pick
  up a swapped brush).

## Left to the builder

(Calls made while writing this up, not discussed with the user, are marked
*designer's call*.)

- *Designer's call:* the scrollbars are themed (the user wasn't asked
  directly; the "nothing left bright" anti-goal settles it).

- How to detect Windows' change live (e.g. `SystemEvents.UserPreferenceChanged`
  or `WM_SETTINGCHANGE` with `"ImmersiveColorSet"`), and how to swap
  palettes (e.g. replacing a merged `ResourceDictionary`).
- Exact dark colours for the preview's elements (links, code blocks,
  quotes, tables) and the hover brush, within option A's olive/yellow family.
- How the theme picker looks in Settings (a combo box like the font
  pickers is the obvious fit) and where it sits (e.g. its own "Appearance"
  group above "Fonts"); Settings window may grow to fit.
- Setting the Settings window's native title bar dark via DWM is
  best-effort: if the call fails (older Windows build), ignore it.
