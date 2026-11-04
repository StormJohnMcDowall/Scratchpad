# Peek Behind Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Peek button and Ctrl+Shift+click briefly hide the note, so the user can see or use whatever is underneath. The note then comes back on its own without taking focus.

**Architecture:**
- **Core (unit-tested):** a `PeekStore` for `peek.json`. It holds the clamping rules and the parsing of the Settings box.
- **Shell (compile-only in the sandbox):**
  - a `DispatcherTimer` in `MainWindow`: `Hide()` starts the peek, and the tick shows the window with `ShowActivated = false`;
  - the existing `ShowAndActivate()` ends a peek, which covers the tray, a second launch and tray Settings… in one place;
  - a tray left-click hook that ends a peek early;
  - a Peek section in Settings.

**Tech Stack:** WPF on .NET Framework 4.8, WinForms `NotifyIcon`, netstandard2.0 Core with xUnit tests.

**Spec:** a small change agreed in chat on 2026-11-04, so there is no spec document. Reproduced:
- **Triggers:**
  - A toolbar Peek button, with the tooltip `Peek behind (Ctrl+Shift+click)`.
  - Ctrl+Shift+click anywhere on the note.
- **While hidden:**
  - The note disappears completely, and clicks reach whatever is underneath.
  - Nothing about the note changes: text, scroll, edit/preview mode, always-on-top and position.
  - Peek works whether or not always-on-top is on.
- **Coming back:**
  - After the peek time (default 3 s), the note reappears in place even if the user is mid-drag or typing underneath.
  - It does **not** take focus.
- **Coming back early:**
  - A tray icon click brings the note back straight away, as does tray → Show.
  - An early return takes focus, like Show.
- **Settings → Peek:**
  - "Peek for [ 3 ] seconds".
  - Whole seconds from 1 to 30; anything outside that is pulled back into range.
  - Saved, and Reset to defaults restores 3.
- **Non-goals:** a system-wide hotkey, fade or see-through effects, peeking the Settings window.

## Global Constraints

- No new NuGet dependencies.
- Exact text:
  - the tooltip `Peek behind (Ctrl+Shift+click)`;
  - `AutomationProperties.Name` `Peek behind`;
  - the Settings group header `Peek`;
  - the row text `Peek for` … `seconds`.
- Seconds: minimum 1, maximum 30, default 3. Persisted at `%LOCALAPPDATA%\Scratchpad\peek.json`.
- The peek's own return must never call `Activate()` and never toggle `Topmost`.
- Ruling on mechanism: use `Hide()`/`Show()`, accepting that the taskbar button blinks.
  - Why: the alternative (a layered window that lets clicks through) depends on WPF and DWM behaviour this sandbox can't run, and the spec ranked the blink as secondary.
  - Cost if wrong: the blink stays.

## Review Focus

1. **Tray clicks.** A left click on the tray icon while *not* peeking must do nothing new. A right click must only open the menu. Double-click still shows the note.
   - Hook `NotifyIcon.MouseClick` with `e.Button == MouseButtons.Left`, not `Click`, which also fires on a right click.
2. **Something else shows the note mid-peek, then the user hides it again within the peek time.** Examples: a second launch, tray Show, tray Settings…, or an early tray click. The stale timer must not bring the note back later.
   - `ShowAndActivate()` stops the timer first (Task 2).
3. **Exit from the tray during a peek.** The timer must not `Show()` a window that is shutting down.
   - `MarkExiting()` stops the timer, and the tick checks `_isExiting` (Task 2).
4. **The peek's own return keeps the user's state.** Always-on-top stays as it was, and focus stays in the other app.
   - Use a separate `ShowWithoutActivating()`. It must not reuse `ShowAndActivate()`'s Topmost toggle (Task 2).
5. **Ctrl+Shift+click lands on a control.** On a preview checkbox, editor text, a toolbar button or a link, the click must only peek. It must not also toggle, select, press or follow.
   - Handle the *tunnelling* `PreviewMouseLeftButtonDown` on the Window and set `e.Handled = true` (Task 2).
6. **The Settings box gets odd input:** `""`, `abc`, `0`, `45`, `2.5`, `99999999999`. Nothing must crash or save an out-of-range value. When the box loses focus, it shows the value actually in use (Task 1 tests, Task 3).

---

### Task 1: PeekStore

**Files:**
- Create: `Scratchpad.Core/PeekSettings.cs`, `Scratchpad.Core/PeekStore.cs`
- Test: `Scratchpad.Core.Tests/PeekStoreTests.cs`

**Interfaces:**
- Produces:
  - `public struct PeekSettings : IEquatable<PeekSettings> { public int Seconds { get; set; } }`
    - Equality, `==` and `!=` follow `DateFormatSettings`.
  - `public class PeekStore`, with:
    - `public const int MinSeconds = 1, MaxSeconds = 30;`
    - `public static PeekSettings Defaults` (Seconds = 3)
    - `public static string DefaultFilePath` (…`Scratchpad\peek.json`)
    - `PeekStore(string filePath)`, `FilePath`
    - `PeekSettings Load()`
    - `void Save(PeekSettings)`
    - `public static int Clamp(int seconds)`
    - `public static int? ParseSeconds(string text)`

- [ ] **Step 1: Write the failing tests** in `PeekStoreTests.cs`, in the style of `DateFormatStoreTests`:

```csharp
[Fact] public void Defaults_SecondsIsThree() => Assert.Equal(3, PeekStore.Defaults.Seconds);
[Fact] public void Load_MissingFile_ReturnsDefaults()   // temp path under a new Guid dir
[Fact] public void Load_CorruptFile_ReturnsDefaults()   // "not json"
[Fact] public void SaveThenLoad_RoundTrips()            // Save Seconds=10 → Load().Seconds == 10

[Theory]
[InlineData(0, 1)]
[InlineData(-5, 1)]
[InlineData(99, 30)]
public void Load_OutOfRangeFile_IsClamped(int saved, int expected)
// Save(new PeekSettings { Seconds = saved }) → Load().Seconds == expected

[Theory]
[InlineData("5", 5)]
[InlineData(" 7 ", 7)]
[InlineData("1", 1)]
[InlineData("30", 30)]
[InlineData("0", 1)]
[InlineData("-3", 1)]
[InlineData("45", 30)]
[InlineData("99999999999", 30)]
public void ParseSeconds_WholeNumber_IsClampedIntoRange(string text, int expected)
// Assert.Equal(expected, PeekStore.ParseSeconds(text))

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
[InlineData("abc")]
[InlineData("2.5")]
public void ParseSeconds_NotAWholeNumber_ReturnsNull(string text)
```

- [ ] **Step 2: Run the tests.** Run `dotnet test Scratchpad.Core.Tests --filter PeekStoreTests`. Expected: a compile failure, because `PeekStore` doesn't exist yet.
- [ ] **Step 3: Implement.**
  - `Load` and `Save` copy `DateFormatStore`: catch-all on Load returns `Defaults`, and Save is an atomic temp-file write with errors swallowed.
  - `Load` returns the loaded value passed through `Clamp`.
  - `ParseSeconds` trims the text and parses with `long.TryParse(…, NumberStyles.Integer, CultureInfo.InvariantCulture, …)`. It clamps a parsed number into range, and returns null otherwise.
- [ ] **Step 4: Run the tests again.** Run `dotnet test Scratchpad.Core.Tests`. Expected: every test passes, including the existing 109.
- [ ] **Step 5: Commit** `feat: store the peek time, clamped to 1-30 seconds`.

### Task 2: Peek in the note and tray

**Files:**
- Modify: `Scratchpad/MainWindow.xaml` (toolbar, Window attributes), `Scratchpad/MainWindow.xaml.cs`, `Scratchpad/TrayIconManager.cs`, `Scratchpad/App.xaml.cs`

**Interfaces:**
- Consumes: `PeekStore`, `PeekSettings` (Task 1).
- Produces:
  - In `MainWindow`:
    - the field `private PeekSettings _peekSettings`, loaded in the constructor next to `_dateFormatSettings`, and the field `_peekStore`;
    - `public void EndPeekEarly()`.
  - The `TrayIconManager` constructor gains a 4th parameter, `Action onLeftClick` (last).

- [ ] **Step 1: Add the toolbar button.**
  - In `MainWindow.xaml`, add it directly after `AlwaysOnTopToggleButton`:
    `<Button x:Name="PeekButton" Content="&#xED1A;" Click="PeekButton_Click" Focusable="False" ToolTip="Peek behind (Ctrl+Shift+click)" AutomationProperties.Name="Peek behind" />`
  - ED1A is MDL2's "Hide" eye. It's distinct from Preview's E890.
- [ ] **Step 2: Add the Ctrl+Shift+click hook.** Add `PreviewMouseLeftButtonDown="Window_PreviewMouseLeftButtonDown"` to the Window. The handler checks `Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)`; if so it calls `StartPeek()` and sets `e.Handled = true` (Review Focus 5).
- [ ] **Step 3: Implement the peek in `MainWindow.xaml.cs`.**
  - Add a `DispatcherTimer _peekTimer`, created in the constructor, whose Tick calls `OnPeekTimerTick`.
  - `private void StartPeek()`:
    - set the interval from `_peekSettings.Seconds`;
    - `Hide()`;
    - start the timer.
  - `OnPeekTimerTick`:
    - stop the timer;
    - return if `_isExiting`;
    - otherwise call `ShowWithoutActivating()`.
  - `private void ShowWithoutActivating()`:
    - `ShowActivated = false; Show(); ShowActivated = true;`
    - nothing else (Review Focus 4);
    - comment that `ShowActivated` decides SW_SHOWNA vs SW_SHOW on every `Show()`.
  - `ShowAndActivate()` calls `_peekTimer.Stop()` first, with a comment pointing at Review Focus 2's scenario.
  - `MarkExiting()` also calls `_peekTimer.Stop()`.
  - `public void EndPeekEarly()`: if `_peekTimer.IsEnabled`, call `ShowAndActivate()`; otherwise do nothing.
  - `PeekButton_Click` calls `StartPeek()`.
- [ ] **Step 4: Add the tray hook.**
  - Add `Action onLeftClick` to `TrayIconManager`.
  - Wire it with `_notifyIcon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) onLeftClick(); };`, plus a one-line comment on why not `Click` (Review Focus 1).
  - In `App.xaml.cs`, pass `onLeftClick: mainWindow.EndPeekEarly`.
  - Tray → Show already ends a peek, through `ShowAndActivate`.
- [ ] **Step 5: Build and test.** Run `dotnet build Scratchpad/Scratchpad.csproj` and `dotnet test Scratchpad.Core.Tests`. Expected: `0 Error(s)`, and all tests pass.
- [ ] **Step 6: Commit** `feat: peek behind the note from the toolbar or Ctrl+Shift+click`.

### Task 3: Peek time in Settings

**Files:**
- Modify: `Scratchpad/SettingsWindow.xaml`, `Scratchpad/SettingsWindow.xaml.cs`, `Scratchpad/MainWindow.xaml.cs` (`OpenSettings`, `OnResetRequested`)

**Interfaces:**
- Consumes: `PeekStore.ParseSeconds`, `PeekStore.Defaults`, `_peekSettings` and `_peekStore` (Tasks 1–2).
- Produces:
  - `event Action<int> PeekSecondsChanged`.
  - The `SettingsWindow` constructor and `ShowSettings` each gain `int peekSeconds`, placed after `dateFormat`.

- [ ] **Step 1: Add the XAML.**
  - Insert a new row 3, `<GroupBox Header="Peek" Margin="8,8,8,0">`, between Date and Fonts. Fonts moves to row 4 and Reset to row 5; add the 6th `Auto` RowDefinition.
  - Inside, a horizontal `StackPanel Margin="8"` holds:
    - `TextBlock "Peek for"`, `VerticalAlignment="Center"`;
    - `TextBox x:Name="PeekSecondsTextBox" Width="40" Margin="6,0" Padding="2" HorizontalContentAlignment="Right"`;
    - `TextBlock "seconds"`.
- [ ] **Step 2: Add the code-behind.**
  - `ShowSettings` sets `PeekSecondsTextBox.Text = peekSeconds.ToString(CultureInfo.CurrentCulture)` inside the existing `_isUpdatingSelection` guard.
  - TextChanged, unless guarded: if `ParseSeconds` returns a value, remember it and raise `PeekSecondsChanged`. Don't rewrite the box while the user types.
  - LostFocus sets the box to the last value in use, so `0` becomes `1` and `abc` returns to the previous number (Review Focus 6).
- [ ] **Step 3: Wire it in MainWindow.**
  - `OpenSettings` passes `_peekSettings.Seconds` and handles `PeekSecondsChanged` the way `DateFormatChanged` is handled: store the new settings and save them.
  - `OnResetRequested` resets to `PeekStore.Defaults`, saves, and passes the value to `ShowSettings`. Add "peek time" to the comment listing what resets.
- [ ] **Step 4: Build and test.** Run `dotnet build Scratchpad/Scratchpad.csproj` and `dotnet test Scratchpad.Core.Tests`. Expected: `0 Error(s)`, and all tests pass.
- [ ] **Step 5: Commit** `feat: set the peek time in Settings`.

## Manual check on Windows (for the user, after merge)

1. With always-on-top on, click Peek. The note vanishes, you can click or drag a window corner underneath, and after ~3 s the note returns in place, still on top. Your typing stays in the other window.
2. Ctrl+Shift+click on the editor text, then on a preview checkbox. Each peeks, and nothing gets selected or ticked.
3. Peek, then single-click the tray icon. The note comes back at once with focus. Wait a few seconds: nothing else happens.
4. With the note shown and not peeking, single-click the tray icon. Nothing happens. Right-click still opens the menu, and double-click still shows the note.
5. Peek, then tray → Show, then hide to tray straight away. The note stays hidden after the peek time has passed.
6. Settings → Peek: type `10`, close Settings, and peek. It lasts ~10 s. Type `0` and Tab away: the box shows `1`. Type `abc` and Tab away: it goes back to the previous number. Reset to defaults sets it to `3`.
7. Peek, then tray → Exit. The app closes cleanly.
