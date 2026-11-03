# Insert Today's Date Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Insert today's date as text at the editor cursor from a toolbar button or Ctrl+;. The format is a .NET date format string that can be set in Settings (default `ddd d MMM`, e.g. "Tue 3 Nov").

**Architecture:**
- In `Scratchpad.Core` (unit-tested):
  - A `DateStamp` formatter: format + date + culture → text, with a fallback to the default format.
  - A `DateFormatStore` that follows the existing settings-store pattern.
- In the Shell:
  - The button and the shortcut.
  - The insertion itself.
  - A Settings "Date" group with a live example.
  - The reset-to-defaults wiring.

**Tech Stack:**
- WPF on .NET Framework 4.8 (Shell, compile-only in the sandbox).
- netstandard2.0 Core with System.Text.Json.
- xUnit.

**Spec:** the agreed description in chat on 2026-11-03 (a small change, so there's no spec document). It is reproduced here:

- **What it inserts:** today's date as plain text at the editor cursor. If text is selected, the date replaces it, as typing would.
- **Format:** a .NET custom date format string. The default `ddd d MMM` gives "Tue 3 Nov". It uses the Windows culture for day and month names.
- **Settings:** a "Date" group with a Format text box and a live example ("Today: Tue 3 Nov").
  - An empty or broken format falls back to the default.
  - Reset to defaults restores `ddd d MMM`.
- **Triggers:**
  - A toolbar button labelled "Date".
  - The **Ctrl+;** shortcut.
- **In preview:** switch to the editor (place kept, as the Preview button does) and insert at the cursor.

## Global Constraints

- No new NuGet dependencies.
- Settings file `%LOCALAPPDATA%\Scratchpad\date-format.json`, using the same best-effort atomic save as `StartupWindowStore`.
- Default format string exactly `ddd d MMM`.
- Formatting must never throw. A `FormatException` or a null/whitespace format falls back to the default.

## Review Focus

1. A format that throws, such as `%` alone or a trailing `\`, must not crash insertion or the Settings example. This is pinned in Task 1.
2. A single-character format like `d` is a .NET *standard* format (the short date). This is accepted as .NET semantics, not a bug.
3. The toolbar button must not take focus from the editor or clear its selection. Use `Focusable="False"`, like the other toolbar buttons.
4. Insertion must be a single undoable edit. Assign via `SelectedText`, not by rebuilding `Text`, which would also lose the scroll position.
5. A dark-mode Settings text box must be dark. Check `Themes/Dark.xaml`'s implicit TextBox style covers Background and Foreground.

---

### Task 1: Core formatter and store

**Files:**
- Create: `Scratchpad.Core/DateStamp.cs`
- Create: `Scratchpad.Core/DateFormatSettings.cs`
- Create: `Scratchpad.Core/DateFormatStore.cs`
- Test: `Scratchpad.Core.Tests/DateStampTests.cs`
- Test: `Scratchpad.Core.Tests/DateFormatStoreTests.cs`

**Interfaces (Produces):**
- `public static class DateStamp`:
  - `public const string DefaultFormat = "ddd d MMM"`
  - `public static string Format(string format, DateTime date, IFormatProvider culture)`
- `public struct DateFormatSettings : IEquatable<DateFormatSettings>` with `string Format { get; set; }`. Follow the shape of `StartupWindowSettings`, including ordinal string equality.
- `public class DateFormatStore`:
  - Members: `Defaults` (Format = `DateStamp.DefaultFormat`), `DefaultFilePath` (`date-format.json`), `FilePath`, ctor `(string filePath)`, `Load()`, `Save(DateFormatSettings)`.
  - `Load()` returns `Defaults` when the file is missing or unreadable, and when the stored Format is null or whitespace.

Steps:
- [ ] **Step 1: Write the failing tests.**
  - `DateStampTests`, all for 2026-11-03 under `CultureInfo.GetCultureInfo("en-GB")`:
    - `Format_DefaultFormat_GivesShortDayDayAndMonth` → `"Tue 3 Nov"`
    - `Format_CustomFormat_IsUsed` (`"yyyy-MM-dd"`) → `"2026-11-03"`
    - `Format_NullOrWhitespace_FallsBackToDefault` (null, `""`, `"  "`) → `"Tue 3 Nov"`
    - `Format_InvalidFormat_FallsBackToDefault` (`"%"`, `"ddd \\"`) → `"Tue 3 Nov"`
  - `DateFormatStoreTests`, following `StartupWindowStoreTests`:
    - `Load_MissingFile_ReturnsDefaults`
    - `SaveThenLoad_RoundTrips` (`"dd/MM/yyyy"`)
    - `Load_CorruptFile_ReturnsDefaults`
    - `Load_BlankFormat_ReturnsDefaults`
    - `Defaults_FormatIsDddDMmm` (== `"ddd d MMM"`)
- [ ] **Step 2: Run the tests to see them fail.** `dotnet test Scratchpad.Core.Tests --filter "DateStamp|DateFormatStore"`. Expected: the build fails because the types don't exist yet.
- [ ] **Step 3: Implement the three files.**
- [ ] **Step 4: Run the tests to see them pass.** `dotnet test Scratchpad.Core.Tests`. Expected: everything passes (91 + 9).
- [ ] **Step 5: Commit.** Message: `feat: add date stamp formatting and persistence`.

### Task 2: Shell — insert, Settings, reset

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`:
  - Add `<Button x:Name="DateButton" Content="Date" Click="DateButton_Click" Padding="8,4" Margin="2,4" Focusable="False" ToolTip="Insert today's date (Ctrl+;)" />`. It goes straight after `AlwaysOnTopToggleButton`, before the separator.
- Modify: `Scratchpad/MainWindow.xaml.cs`:
  - Add a `_dateFormatStore` field, plus `_dateFormatSettings` loaded in the constructor.
  - Add `private void InsertDate()`. If the preview is visible, call `SwitchToEditorAt(null)`. Then:
    - `stamp = DateStamp.Format(_dateFormatSettings.Format, DateTime.Now, CultureInfo.CurrentCulture)`
    - `EditorTextBox.SelectedText = stamp`
    - `EditorTextBox.CaretIndex = EditorTextBox.SelectionStart + stamp.Length` (this collapses the selection)
    - `EditorTextBox.Focus()`
  - Ctrl+; goes in `Window_PreviewKeyDown`: `Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemSemicolon`.
  - Settings wiring: a new `DateFormatChanged` handler saves the setting. Reset sets `_dateFormatSettings = DateFormatStore.Defaults`, saves, and passes it to `ShowSettings`.
- Modify: `Scratchpad/SettingsWindow.xaml`:
  - Add a new "Date" GroupBox row after Startup, with a Label "Format" (100-wide column, like Theme), a `DateFormatTextBox`, and below it a `DateExampleTextBlock`.
  - Grow `Height` by about 70.
- Modify: `Scratchpad/SettingsWindow.xaml.cs`:
  - The constructor and `ShowSettings` gain a `string dateFormat` parameter.
  - Add `public event Action<string> DateFormatChanged`, raised on TextChanged (guarded by `_isUpdatingSelection`).
  - The example updates on every change to `"Today: " + DateStamp.Format(text, DateTime.Now, CultureInfo.CurrentCulture)`.

Steps:
- [ ] **Step 1: Implement the changes above.**
- [ ] **Step 2: Check the Dark.xaml TextBox style** (Review Focus 5). If it doesn't cover the Settings box, add what's missing in dark only.
- [ ] **Step 3: Build the Shell.** `dotnet build Scratchpad/Scratchpad.csproj`. Expected: `Build succeeded`, 0 errors.
- [ ] **Step 4: Run the Core tests.** `dotnet test Scratchpad.Core.Tests`. Expected: everything passes.
- [ ] **Step 5: Commit.** Message: `feat: insert today's date from toolbar and Ctrl+;`.

## Manual test script (Windows)

1. Click **Date** with the cursor mid-line. "Tue 3 Nov" (today's date) is inserted there, and the cursor sits after it.
2. Select a word and press **Ctrl+;**. The date replaces the word. **Ctrl+Z** brings the word back.
3. In preview, press **Ctrl+;**. The editor opens with your place kept, and the date is inserted at the cursor.
4. In Settings, type `yyyy-MM-dd`. The example updates as you type, and **Date** now inserts `2026-11-03`. Type `%`. The example shows the default and nothing crashes.
5. Click **Reset to defaults**. The format box shows `ddd d MMM`.
6. Restart the app. The custom format is remembered. Then check dark mode: the Settings text box is dark.
