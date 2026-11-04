# Global Hotkey — Design

## Intent

The user is working in another app when a thought comes up and wants it
written down straight away. Today the note is only reachable with the
mouse, through the tray icon or the taskbar button. The user's words:
*"When I'm in another app and want to jot something quickly."*

A system-wide key combination brings the note forward, ready to type, from
any app. The default is **Win+Shift+N**, and the user can change it in
Settings.

## Goals

- **Pressing the hotkey anywhere** brings the note forward with keyboard
  focus in the editor, so typing works immediately. It works in every state
  the note can be in:
  - hidden to the tray (including a start that never showed the window);
  - behind other windows;
  - minimised;
  - away on a peek. The peek ends and the note returns home, as every other
    way of showing the note already does through `ShowAndActivate`.
- **Preview → editor.** If the note is showing the preview, it switches to
  the editor and keeps your place, the same way Ctrl+E does
  (`SwitchToEditorAt(null)`). *Designer's call, agreed in playback.*
- **Where the cursor lands.** It never jumps to the end of the note.
  - **Editor already showing:** the cursor stays exactly where the user
    left it. *Designer's call, agreed in playback.*
  - **Preview showing:** the cursor lands at the start of the block at the
    top of the preview, exactly as Ctrl+E does, so typing starts where the
    user was reading. The user chose this over restoring the cursor from
    before the switch to preview. Use `SwitchToEditorAt(null)` as it is;
    don't add code to remember the earlier cursor position.
- **The hotkey only shows the note.** Pressing it while the note is already
  in front does nothing more (focus goes to the editor). The user leaves
  with Alt+Tab as usual: *"just alt-tab probably."*
- **Default key: Win+Shift+N.** The user asked for a Win-key combination
  because *"all the ctrl/shift/alt will maybe be taken by some app (eg IDEs
  have a million shortcuts)"*. A global hotkey is taken before the app in
  front sees it, so a Ctrl/Alt combination would quietly break that
  shortcut in IDEs. Plain Win+letter is mostly reserved by Windows, so the
  default adds Shift. *Designer's pick of N, accepted by the user.*
- **Changeable in Settings** (the user: *"Need to be able to change"*). A
  new **Hotkey** group box in Settings has:
  - **A capture box** showing the current key as text, e.g.
    `Win+Shift+N`. Click or focus it, press a combination, and it records
    exactly that, like setting a shortcut in VS Code or Teams. While
    listening it shows a prompt, e.g. "Press a key combination…".
  - **A None button** that turns the hotkey off. The box then shows
    "None".
  - **A message line** below, in the same style as the Date section's
    example line. It warns in `SettingsWarningBrush` when a key can't be
    used.
- **A change takes effect immediately.** If Windows refuses the new
  combination because something else owns it, the warning line says so
  (e.g. "Win+Shift+N is in use by another program") and **the previous key
  stays registered and saved**, so the user is never left without a working
  hotkey by accident.
- **Validation.** A combination must include at least one of Win, Ctrl or
  Alt, so a plain or Shift-only letter can never be grabbed (that would
  break typing everywhere). A modifier pressed alone is not a combination:
  keep listening until a non-modifier key arrives.
- **Esc cancels capture** and leaves the key as it was.
- **Reset to defaults** sets the key back to Win+Shift+N, following the same
  rule (if that's taken, warn and keep the current key).
- **Persisted in `hotkey.json`** in the same folder as the other settings
  files, following the existing store pattern (`PeekStore` and the others):
  `Defaults`, a `DefaultFilePath`, atomic save via temp + `File.Replace`, and
  a load that falls back to the defaults on any error or nonsense value.
  "None" is stored explicitly, so a turned-off hotkey stays off.
- **At startup,** Scratchpad registers the saved key. If Windows refuses it,
  Scratchpad shows **one tray notification** per launch (e.g. "Scratchpad
  couldn't use Win+Shift+N — another program has it. Choose a different
  key in Settings."). Opening Settings then shows the same warning on the
  message line.

## Non-goals

- No hide/toggle on a second press. Alt+Tab is how you leave.
- No second hotkey (e.g. one for "peek" or "insert date").
- No jump-to-end or "new line at the bottom" behaviour.
- No help working out *which* program owns a taken key. Windows doesn't say.

## Anti-goals

- **Never break typing.** A hotkey without Win, Ctrl or Alt must be
  impossible to save.
- **Never fail silently.** If the hotkey isn't working because Windows
  refused it, the user is told (tray notification at startup, warning line
  in Settings).
- **Never leave the user with no hotkey without them choosing None.** A
  failed change keeps the old one.
- **A hotkey failure must never crash or stop the app.** Registration,
  notification and storage are niceties, so wrap them like the other
  settings (see the project's history of startup/shutdown crashes that a
  build could not reveal).

## Constraints

- WPF on .NET Framework 4.8 (`Scratchpad`), `Scratchpad.Core` on
  netstandard2.0. No new NuGet dependencies and no admin rights. Global
  hotkeys use Win32 `RegisterHotKey`/`UnregisterHotKey` through P/Invoke
  (user32), which needs no elevation.
- The Shell can only be compiled in the dev sandbox, never run. The parts
  that can be tested belong in `Scratchpad.Core` with xUnit tests:
  - the hotkey value (modifiers plus a key, or None) and its equality;
  - turning it into display text (`Win+Shift+N`, `Ctrl+Alt+F5`, `None`);
  - the validation rule (needs Win/Ctrl/Alt; a modifier alone isn't a key);
  - the store (load/save/defaults/bad-file fallback).

  Core can't reference WPF's `Key` enum, so represent the key as a Win32
  virtual-key code (an int), which is also what `RegisterHotKey` takes. The
  Shell converts with `KeyInterop.VirtualKeyFromKey`.
- **The app usually starts hidden** (Settings → Startup is off by default),
  so the hotkey must be registered without the main window ever having
  been shown. `RegisterHotKey` needs a window handle that receives
  `WM_HOTKEY`.
- **While the capture box is listening, the current hotkey must be
  suspended.** Otherwise pressing the same (or the currently registered)
  combination fires the hotkey instead of reaching the box. Re-register
  when capture ends: on success, cancel (Esc), losing focus or closing
  Settings.
- **WPF keyboard quirks the capture box must handle:**
  - With Alt held, `KeyEventArgs.Key` is `Key.System` and the real key is
    in `e.SystemKey`.
  - The Win key arrives as `Key.LWin`/`Key.RWin`. `Keyboard.Modifiers`
    includes `ModifierKeys.Windows` while it's held.
  - Windows-reserved combinations (Win+L, Win+D, …) never reach the app.
    They simply can't be recorded, which is fine.
- **Foreground rights.** Windows lets the process that receives a
  `WM_HOTKEY` take the foreground, so `ShowAndActivate` (with its existing
  Topmost-toggle fallback) is expected to bring the note in front.
  Confirm this on the real machine.
- Unregister the hotkey on exit (tray Exit) along with the other cleanup in
  `App`.
- Tray notifications use the existing WinForms `NotifyIcon` in
  `TrayIconManager` (e.g. `ShowBalloonTip`). On Windows 10/11 this shows as
  a toast.

## Left to the builder

- **Which window handle to register against.** For example, the main
  window's handle created early with `WindowInteropHelper.EnsureHandle()`,
  or a small dedicated message-only `HwndSource` owned by `App`. Either way
  it must exist before the first `Show()` and survive Hide/Show cycles.
- **Swapping keys safely.** Changing from the old key to the new one
  without a moment where neither is registered, or where the old one is
  lost if the new one fails. For example, use two hotkey IDs, or unregister
  the old key, try the new one and re-register the old one on failure.
- **Hooking the hotkey into the window.** The handler calls
  `ShowAndActivate`, then switches to the editor if needed and focuses it.
  Reuse existing code rather than adding a parallel show path. If the
  Settings window is open, bring the note forward without closing Settings.
- **Exact wording** of the prompt, the warning line and the tray message.
- **Tab in the capture box.** *Designer's call:* Tab and Shift+Tab without
  Win/Ctrl/Alt move focus as usual instead of being recorded (they're
  invalid hotkeys anyway). That keeps Settings usable from the keyboard.
- **Where the group box goes.** After Peek and before Fonts fits the
  existing order. Settings is `SizeToContent="Height"`, so it simply grows.
- **Dark mode:** the capture box and None button must look right in dark
  mode. Check against how the Date and Peek boxes are themed in
  `Themes/Dark.xaml`.

## Manual checks on Windows

These can't run in the sandbox, so they're for the user after merge:

1. Start with the window hidden. From another app (e.g. a browser), press
   Win+Shift+N: the note appears in front with the cursor in the editor, and
   typing goes into the note.
2. With the note behind other windows, minimised, and in preview mode, the
   hotkey brings it forward in the editor each time. From the editor the
   cursor is where it was; from the preview it's at the start of the
   paragraph that was at the top, as with Ctrl+E.
3. Press the hotkey during a peek: the note comes straight back.
4. In Settings, record Ctrl+Alt+F12 (or similar): the box shows it, the old
   key stops working and the new one works at once. Restart: the new key
   still works.
5. Try to record a combination another program owns (or a plain letter, or
   Shift+letter): a warning shows and the previous key still works.
6. Esc while recording leaves the key unchanged. None turns it off, and
   that survives a restart.
7. Reset to defaults puts back Win+Shift+N.
8. Make the saved key unavailable (e.g. another program grabs it), then
   restart: one tray notification appears, and Settings shows the warning.
9. Win+Shift+N doesn't open the Start menu or type an "N" in the app in
   front.
10. Everything in Settings looks right in dark mode.
