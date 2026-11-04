# Global Hotkey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A system-wide hotkey brings the note forward, ready to type, from any app. The default is Win+Shift+N, and the key can be changed in Settings.

**Architecture:**
- **Core (unit-tested):**
  - the hotkey value: modifiers plus a Win32 virtual-key code, or None;
  - its display text;
  - the validation and capture rules;
  - a `HotkeyStore` for `hotkey.json`.
- **Shell (compile-only in the sandbox):**
  - a `GlobalHotkey` class that owns a message-only `HwndSource` and calls `RegisterHotKey`/`UnregisterHotKey`;
  - `MainWindow` wiring: load the setting, start the hotkey, handle a press, clean up on exit;
  - a tray balloon when the hotkey can't be registered at startup;
  - a Hotkey section in Settings with a capture box and a None button.

**Tech Stack:** WPF on .NET Framework 4.8, user32 P/Invoke, WinForms `NotifyIcon`, netstandard2.0 Core with xUnit tests.

**Spec:** `docs/superpowers/specs/2026-11-04-global-hotkey-design.md`. Read it first: the goals, the anti-goals and the "Constraints" section all apply.

## Global Constraints

- No new NuGet dependencies, and no admin rights.
- Default hotkey: **Win+Shift+N**. That is `HotkeyModifiers.Win | HotkeyModifiers.Shift`, virtual key `0x4E`.
- Persisted at `%LOCALAPPDATA%\Scratchpad\hotkey.json`. "None" is stored explicitly as `VirtualKey = 0`.
- A saved or registered hotkey must include Win, Ctrl or Alt.
- Hotkey failures (registering, the balloon, the store) must never throw out to the app. Every Shell entry point catches and degrades.
- Exact copy:
  - Settings group header: `Hotkey`.
  - Label beside the capture box: `Show note`.
  - Button: `None`.
  - Capture-box prompt while listening: `Press a key combination…`.
  - Info line when the hotkey is working: `Brings the note forward from any app.`
  - Info line when it's off: `No hotkey.`
  - Warning when the combination has no Win/Ctrl/Alt: `Include Win, Ctrl or Alt.`
  - Warning when Windows refuses a chosen key: `{chosen} is in use by another program. Still using {current}.`
    - When the current key is None, that sentence ends `Still no hotkey.` instead.
  - Warning when the saved key isn't registered (startup failure or a failed resume): `{current} is in use by another program, so it isn't working. Choose another key.`
  - Tray balloon at startup: title `Scratchpad`, text `Couldn't use {current} as the hotkey because another program has it. Choose another key in Settings.`
- Build and test commands (dotnet 10 is installed in the sandbox):
  - `dotnet test Scratchpad.Core.Tests`
  - `dotnet build Scratchpad/Scratchpad.csproj`, which compiles the WPF shell thanks to `EnableWindowsTargeting`.

## Review Focus

1. **Capturing the current key.** In Settings the user presses the hotkey that's already registered, e.g. Win+Shift+N. The box must record it rather than the note popping forward.
   - The hotkey is suspended while listening (Task 4), and the window raises `HotkeyCaptureEnded` *before* `HotkeyChosen` so the old key is back in place before the swap.
2. **Leaving the box without choosing.** The user tabs out, clicks elsewhere, presses Esc, or closes Settings while it's listening. Each of these must re-register the old key; otherwise the hotkey silently dies until restart (Task 4).
3. **A refused change.** The new key is taken by another program. The old key must still be registered and must still be the saved value (Task 3 `TryChange` contract, Task 4).
4. **First press after a hidden start.** The window has never been shown, so `Window_Loaded` runs inside `ShowAndActivate()`. The editor must still end up with keyboard focus. Defer the focus with `Dispatcher.BeginInvoke(DispatcherPriority.Input, …)` (Task 3).
5. **A hand-edited `hotkey.json`** with modifiers but no Win/Ctrl/Alt (e.g. Shift+A), a modifier as the key, or out-of-range numbers. It must load as the default, never register a typing-breaking key, and never crash (Task 2 tests).

---

### Task 1: Hotkey value, text and capture rules

**Files:**
- Create: `Scratchpad.Core/HotkeySettings.cs`, `Scratchpad.Core/Hotkey.cs`
- Test: `Scratchpad.Core.Tests/HotkeyTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] public enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }`.
    - These values equal Win32's `MOD_ALT`, `MOD_CONTROL`, `MOD_SHIFT` and `MOD_WIN`, so the Shell can pass them straight to `RegisterHotKey`.
  - `public struct HotkeySettings : IEquatable<HotkeySettings>`, with:
    - `HotkeyModifiers Modifiers { get; set; }`
    - `int VirtualKey { get; set; }`
    - `bool IsNone => VirtualKey == 0`
    - `static HotkeySettings None`
    - Equality, `==` and `!=` in the style of `PeekSettings`.
  - `public enum HotkeyKeyAction { KeepListening, Cancel, MoveFocus, NeedsModifier, Accept }`
  - `public static class Hotkey`, with:
    - `bool IsValid(HotkeySettings hotkey)`: true when the key isn't None, the virtual key is in `1..0xFE`, the key isn't itself a modifier, and the modifiers include Win, Control or Alt.
    - `bool IsModifierKey(int virtualKey)`: true for `0x10`–`0x12`, `0x5B`, `0x5C` and `0xA0`–`0xA5`.
    - `HotkeyKeyAction Classify(HotkeyModifiers modifiers, int virtualKey)`. Rules, checked in this order:
      1. A modifier key → `KeepListening`.
      2. Esc (`0x1B`) with no modifiers → `Cancel`.
      3. Tab (`0x09`) without Win/Ctrl/Alt → `MoveFocus`.
      4. No Win/Ctrl/Alt → `NeedsModifier`.
      5. Anything else → `Accept`.
    - `string Format(HotkeySettings hotkey)`:
      - None gives `"None"`.
      - Otherwise the modifiers come in the order `Win+Ctrl+Alt+Shift+`, followed by `KeyName(VirtualKey)`.
    - `string KeyName(int virtualKey)`:
      - `A`–`Z` (`0x41`–`0x5A`) and `0`–`9` (`0x30`–`0x39`) as the character.
      - `F1`–`F24` (`0x70`–`0x87`).
      - `Num 0`–`Num 9` (`0x60`–`0x69`).
      - `Space` 0x20, `Enter` 0x0D, `Backspace` 0x08, `Tab` 0x09, `Esc` 0x1B.
      - `Insert` 0x2D, `Delete` 0x2E, `Home` 0x24, `End` 0x23, `PageUp` 0x21, `PageDown` 0x22.
      - `Left` 0x25, `Up` 0x26, `Right` 0x27, `Down` 0x28.
      - `Pause` 0x13, `PrintScreen` 0x2C.
      - The US-layout punctuation names: 0xBA `;`, 0xBB `=`, 0xBC `,`, 0xBD `-`, 0xBE `.`, 0xBF `/`, 0xC0 `` ` ``, 0xDB `[`, 0xDC `\`, 0xDD `]`, 0xDE `'`.
      - Anything else as `Key 0x{vk:X2}`.
      - Accepted limitation: punctuation is named by its US key cap even on a UK keyboard.

- [ ] **Step 1: Write the failing tests** in `HotkeyTests.cs`:

```csharp
[Fact] public void None_IsNone() => Assert.True(HotkeySettings.None.IsNone);
[Fact] public void Modifiers_MatchWin32Values()
// (int)Alt == 1, Control == 2, Shift == 4, Win == 8

[Theory]
[InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, "Win+Shift+N")]
[InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x74, "Ctrl+Alt+F5")]
[InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Win, 0x20, "Win+Ctrl+Alt+Shift+Space")]
[InlineData(HotkeyModifiers.Control, 0x63, "Ctrl+Num 3")]
[InlineData(HotkeyModifiers.Alt, 0xBA, "Alt+;")]
[InlineData(HotkeyModifiers.Win, 0xE7, "Win+Key 0xE7")]
public void Format_NamesModifiersInOrderThenKey(HotkeyModifiers mods, int vk, string expected)

[Fact] public void Format_None_IsNone() => Assert.Equal("None", Hotkey.Format(HotkeySettings.None));

[Theory]
[InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, true)]
[InlineData(HotkeyModifiers.Control, 0x41, true)]
[InlineData(HotkeyModifiers.Alt, 0x70, true)]
[InlineData(HotkeyModifiers.Shift, 0x41, false)]   // Shift-only would break typing
[InlineData(HotkeyModifiers.None, 0x41, false)]
[InlineData(HotkeyModifiers.Control, 0x10, false)] // a modifier as the key
[InlineData(HotkeyModifiers.Win, 0xA2, false)]
[InlineData(HotkeyModifiers.Win, 0, false)]        // None isn't a valid key to register
[InlineData(HotkeyModifiers.Win, 0xFF, false)]
[InlineData(HotkeyModifiers.Win, -1, false)]
public void IsValid_RequiresWinCtrlOrAltAndARealKey(HotkeyModifiers mods, int vk, bool expected)

[Theory]
[InlineData(HotkeyModifiers.Win, 0x5B, HotkeyKeyAction.KeepListening)]          // Win key itself
[InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0xA0, HotkeyKeyAction.KeepListening)]
[InlineData(HotkeyModifiers.None, 0x1B, HotkeyKeyAction.Cancel)]
[InlineData(HotkeyModifiers.Control, 0x1B, HotkeyKeyAction.Accept)]               // Ctrl+Esc is a combination
[InlineData(HotkeyModifiers.None, 0x09, HotkeyKeyAction.MoveFocus)]
[InlineData(HotkeyModifiers.Shift, 0x09, HotkeyKeyAction.MoveFocus)]
[InlineData(HotkeyModifiers.Control, 0x09, HotkeyKeyAction.Accept)]
[InlineData(HotkeyModifiers.None, 0x41, HotkeyKeyAction.NeedsModifier)]
[InlineData(HotkeyModifiers.Shift, 0x41, HotkeyKeyAction.NeedsModifier)]
[InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, HotkeyKeyAction.Accept)]
public void Classify_FollowsCaptureRules(HotkeyModifiers mods, int vk, HotkeyKeyAction expected)
```

- [ ] **Step 2: Run the tests.** Run `dotnet test Scratchpad.Core.Tests --filter HotkeyTests`. Expected: a compile failure, because the types don't exist yet.
- [ ] **Step 3: Implement** `HotkeySettings.cs` (the enum, the struct and `HotkeyKeyAction`) and `Hotkey.cs` to the Interfaces above.
- [ ] **Step 4: Run the tests again.** Run `dotnet test Scratchpad.Core.Tests`. Expected: every test passes, the existing ones included.
- [ ] **Step 5: Commit** `feat: hotkey value, display text and capture rules`.

### Task 2: HotkeyStore

**Files:**
- Create: `Scratchpad.Core/HotkeyStore.cs`
- Test: `Scratchpad.Core.Tests/HotkeyStoreTests.cs`

**Interfaces:**
- Consumes: `HotkeySettings`, `HotkeyModifiers` and `Hotkey.IsValid` (Task 1).
- Produces: `public class HotkeyStore`, with:
  - `static HotkeySettings Defaults` (Win|Shift, `0x4E`)
  - `static string DefaultFilePath` (…`Scratchpad\hotkey.json`)
  - `HotkeyStore(string filePath)` and `FilePath`
  - `HotkeySettings Load()`
  - `void Save(HotkeySettings)`

- [ ] **Step 1: Write the failing tests** in `HotkeyStoreTests.cs`, in the style of `PeekStoreTests`:

```csharp
[Fact] public void Defaults_IsWinShiftN()   // Modifiers == Win|Shift, VirtualKey == 0x4E
[Fact] public void Load_MissingFile_ReturnsDefaults()
[Fact] public void Load_CorruptFile_ReturnsDefaults()   // "not json"
[Fact] public void SaveThenLoad_RoundTrips()            // Ctrl|Alt + 0x74
[Fact] public void SaveThenLoad_None_StaysNone()        // Save(None) → Load().IsNone; a turned-off hotkey stays off
[Fact] public void Load_NoneWithStrayModifiers_IsPlainNone()
// file {"Modifiers":8,"VirtualKey":0} → Load() == HotkeySettings.None

[Theory]
[InlineData("{\"Modifiers\":4,\"VirtualKey\":65}")]     // Shift+A would break typing
[InlineData("{\"Modifiers\":0,\"VirtualKey\":65}")]
[InlineData("{\"Modifiers\":2,\"VirtualKey\":17}")]     // Ctrl as the key
[InlineData("{\"Modifiers\":8,\"VirtualKey\":999}")]
[InlineData("{\"Modifiers\":8,\"VirtualKey\":-4}")]
public void Load_InvalidHotkey_ReturnsDefaults(string json)
```

- [ ] **Step 2: Run the tests.** Run `dotnet test Scratchpad.Core.Tests --filter HotkeyStoreTests`. Expected: a compile failure.
- [ ] **Step 3: Implement.**
  - Copy `PeekStore`'s Load/Save shape: catch-all Load returns `Defaults`, and Save is an atomic temp-file write with errors swallowed.
  - `Load`:
    - `VirtualKey == 0` returns `HotkeySettings.None`;
    - otherwise a value that passes `Hotkey.IsValid` is returned as loaded;
    - anything else returns `Defaults`.
- [ ] **Step 4: Run the tests again.** Run `dotnet test Scratchpad.Core.Tests`. Expected: all pass.
- [ ] **Step 5: Commit** `feat: store the hotkey, defaulting to Win+Shift+N`.

### Task 3: Register the hotkey and show the note on press

**Files:**
- Create: `Scratchpad/GlobalHotkey.cs`
- Modify: `Scratchpad/MainWindow.xaml.cs`, `Scratchpad/TrayIconManager.cs`, `Scratchpad/App.xaml.cs`

**Interfaces:**
- Consumes: `HotkeySettings`, `Hotkey`, `HotkeyStore` (Tasks 1–2).
- Produces:
  - `public sealed class GlobalHotkey : IDisposable`, with:
    - `event Action Pressed`
    - `HotkeySettings Current { get; }`: the user's chosen key, whether or not it's registered.
    - `bool IsRegistered { get; }`: true when `Current` is registered. Always false for None.
    - `bool Start(HotkeySettings settings)`: sets `Current` and registers it unless None. Returns `IsRegistered || Current.IsNone`.
    - `bool TryChange(HotkeySettings next)`:
      - None: unregister, set `Current = None`, return true.
      - Equal to `Current` and registered: return true.
      - Otherwise: register `next` under the spare ID. On success, unregister the old ID, set `Current = next` and return true. On failure, change nothing and return false.
      - The old key is never released before the new one is held.
    - `void Suspend()`: unregisters `Current` while the Settings box listens. `IsRegistered` becomes false.
    - `bool Resume()`: re-registers `Current` if suspended, and returns `IsRegistered || Current.IsNone`.
    - `Dispose()`: unregisters and disposes the `HwndSource`.
  - `TrayIconManager.ShowWarning(string text)`: `ShowBalloonTip(5000, "Scratchpad", text, ToolTipIcon.Warning)`, wrapped in try/catch.
  - In `MainWindow`:
    - `private readonly HotkeyStore _hotkeyStore = new HotkeyStore(HotkeyStore.DefaultFilePath);`
    - `private GlobalHotkey _globalHotkey;`, created in the constructor next to the other loads.
    - `public void StartGlobalHotkey(Action<string> showWarning)`
    - `internal GlobalHotkey GlobalHotkey => _globalHotkey;` for Task 4.

- [ ] **Step 1: Implement `GlobalHotkey`.**
  - It creates a message-only window: `new HwndSource(new HwndSourceParameters("ScratchpadHotkey") { ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */, Width = 0, Height = 0, WindowStyle = 0 })`, then `AddHook`.
  - The hook raises `Pressed` on `WM_HOTKEY` (`0x0312`) when the ID is one of the class's two IDs (e.g. `1` and `2`, alternating), and marks the message handled.
  - It registers with `(uint)Modifiers | MOD_NOREPEAT (0x4000)` and `VirtualKey`.
  - It declares `RegisterHotKey`/`UnregisterHotKey` from user32 (`SetLastError = true`).
  - Every public member catches exceptions. A failed `HwndSource` creation leaves an instance whose registrations all fail (returning false) rather than throwing.
  - Comment why it uses a message-only window: the app usually starts hidden, and the handle must outlive Hide/Show.
- [ ] **Step 2: Add `ShowWarning` to `TrayIconManager`.**
- [ ] **Step 3: Wire `MainWindow`.**
  - Constructor: `_globalHotkey = new GlobalHotkey(); _globalHotkey.Pressed += OnGlobalHotkeyPressed;`.
  - `StartGlobalHotkey(showWarning)` loads `_hotkeyStore.Load()` and calls `Start`. On false, it calls `showWarning` with the tray copy from Global Constraints, filled in with `Hotkey.Format(current)`. It is wrapped in try/catch.
  - `OnGlobalHotkeyPressed()`:
    - calls `ShowAndActivate()`;
    - if `PreviewToggleButton.IsChecked == true`, calls `SwitchToEditorAt(null)` (the user's choice: caret at the top preview block, as Ctrl+E does);
    - then calls `Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => EditorTextBox.Focus()))`, so focus also lands on the first show after a hidden start (Review Focus 4);
    - doesn't touch the caret or the Settings window.
  - `MarkExiting()` also calls `_globalHotkey.Dispose()`.
- [ ] **Step 4: Wire `App.OnStartup`.** After the tray is created and before the final show, call `mainWindow.StartGlobalHotkey(tray.ShowWarning);`.
- [ ] **Step 5: Build.** Run `dotnet build Scratchpad/Scratchpad.csproj`, then `dotnet test Scratchpad.Core.Tests`. Expected: the build succeeds with 0 errors, and all tests pass.
- [ ] **Step 6: Commit** `feat: Win+Shift+N brings the note forward from any app`.

### Task 4: Hotkey section in Settings

**Files:**
- Modify: `Scratchpad/SettingsWindow.xaml`, `Scratchpad/SettingsWindow.xaml.cs`, `Scratchpad/MainWindow.xaml.cs` (`OpenSettings`, `OnResetRequested`)

**Interfaces:**
- Consumes:
  - `Hotkey.Classify`, `Hotkey.Format`, `HotkeySettings`, `HotkeyModifiers` (Task 1);
  - `HotkeyStore` (Task 2);
  - `GlobalHotkey` and `MainWindow._hotkeyStore` (Task 3).
- Produces, on `SettingsWindow`:
  - The constructor gains `HotkeySettings hotkey, bool hotkeyWorking`, after `peekSeconds`.
  - `event Action HotkeyCaptureStarted`, `event Action HotkeyCaptureEnded`, `event Action<HotkeySettings> HotkeyChosen`.
  - `public void ShowHotkey(HotkeySettings current, bool working, HotkeySettings? refused)`: refreshes the box and the message line using the copy from Global Constraints.
    - `refused` is non-null only right after a failed change, and then shows the "in use … Still using" warning.
    - `!working` with a non-None `current` shows the "isn't working" warning.
    - Otherwise it shows the info line (or `No hotkey.` for None).
    - Warnings use `SetResourceReference(TextBlock.ForegroundProperty, "SettingsWarningBrush")`; info lines use `ClearValue`, like `UpdateDateExample`.
  - `ShowSettings(...)` is unchanged. The reset path calls `ShowHotkey` separately.

- [ ] **Step 1: Add the XAML.**
  - Insert a new row `Grid.Row="4"` with `GroupBox Header="Hotkey"` between Peek and Fonts, and renumber Fonts to 5 and the Reset button to 6 (add a seventh `RowDefinition`).
  - Inside, use the Date section's two-column grid (100 / *):
    - Label `Show note`.
    - A `DockPanel` holding a right-docked `Button x:Name="HotkeyNoneButton" Content="None" Padding="8,2" Margin="6,0,0,0"` and a `TextBox x:Name="HotkeyTextBox" IsReadOnly="True" IsReadOnlyCaretVisible="False" Cursor="Arrow" VerticalContentAlignment="Center" Padding="2"`.
    - In row 1, `TextBlock x:Name="HotkeyMessageTextBlock" Margin="2,4,0,0" TextWrapping="Wrap"`.
  - Dark mode comes from `Dark.xaml`'s existing implicit TextBox and Button styles. Check that nothing local overrides them.
- [ ] **Step 2: Implement capture in `SettingsWindow.xaml.cs`.** It needs a `_listening` flag and the current/working state.
  - **Start listening** on the box's `GotKeyboardFocus` and on `PreviewMouseLeftButtonDown`, when not already listening:
    - set `_listening`;
    - raise `HotkeyCaptureStarted`;
    - show `Press a key combination…` in the box and clear the message line.
  - **`PreviewKeyDown` on the box, while listening:**
    - Read the key as `e.Key == Key.System ? e.SystemKey : e.Key`, and the virtual key as `KeyInterop.VirtualKeyFromKey(key)`.
    - Map `Keyboard.Modifiers` to `HotkeyModifiers` (Windows → Win, Control, Alt, Shift).
    - Then switch on `Hotkey.Classify`:
      - `KeepListening`: `e.Handled = true`.
      - `Cancel`: `e.Handled = true`, then stop listening and show the current key.
      - `MoveFocus`: leave it unhandled, so Tab moves focus and `LostKeyboardFocus` stops listening.
      - `NeedsModifier`: `e.Handled = true`, show the `Include Win, Ctrl or Alt.` warning, and keep listening.
      - `Accept`: `e.Handled = true`, then **stop listening (raising `HotkeyCaptureEnded`) first**, then raise `HotkeyChosen(new HotkeySettings { Modifiers = …, VirtualKey = … })`. This order is Review Focus 1.
  - **Stop listening** on `LostKeyboardFocus`, and on the window's `Closed` when `_listening`: clear the flag, raise `HotkeyCaptureEnded`, and show the current key. Stopping is idempotent.
  - **None button:** stop listening if needed, then `HotkeyChosen(HotkeySettings.None)`. The button keeps `Focusable` so keyboard users can reach it.
- [ ] **Step 3: Wire `MainWindow.OpenSettings`.**
  - Pass `_globalHotkey.Current, _globalHotkey.IsRegistered || _globalHotkey.Current.IsNone` to the constructor.
  - Subscribe:
    - `HotkeyCaptureStarted += () => _globalHotkey.Suspend();`
    - `HotkeyCaptureEnded += () => { _globalHotkey.Resume(); _settingsWindow?.ShowHotkey(_globalHotkey.Current, _globalHotkey.IsRegistered || _globalHotkey.Current.IsNone, null); };`
      - This way a resume that fails (because another program grabbed the key mid-capture) shows the "isn't working" warning instead of passing silently.
    - `HotkeyChosen += ApplyChosenHotkey;`
  - Add `private void ApplyChosenHotkey(HotkeySettings chosen)`:
    - On `_globalHotkey.TryChange(chosen)` success: `_hotkeyStore.Save(chosen)`, then `_settingsWindow?.ShowHotkey(chosen, true, null)`.
    - On failure: `_settingsWindow?.ShowHotkey(_globalHotkey.Current, _globalHotkey.IsRegistered || _globalHotkey.Current.IsNone, chosen)`.
    - The saved value never changes on failure (anti-goal: never left without a hotkey by accident).
- [ ] **Step 4: Reset.** In `OnResetRequested`, after the peek reset, call `ApplyChosenHotkey(HotkeyStore.Defaults)`. A refused default warns and keeps the current key, per the spec.
- [ ] **Step 5: Build and test.** Run `dotnet build Scratchpad/Scratchpad.csproj` and `dotnet test Scratchpad.Core.Tests`. Expected: 0 errors, and all tests pass.
- [ ] **Step 6: Commit** `feat: change or turn off the hotkey in Settings`.

---

Manual checks on Windows after merge: the spec's "Manual checks on Windows" list, items 1–10.
