# Reset to Defaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A "Reset to defaults" button in the Settings window that, after a Yes/No confirmation, resets font families and font sizes to their out-of-the-box values, applies them immediately, and saves them.

**Architecture:** The two Core stores expose their existing private default settings as public static properties, so a reset uses exactly the values a fresh install gets. `SettingsWindow` owns the button and the confirmation and raises a `ResetRequested` event; `MainWindow` (which owns both settings objects and their stores) does the reset, applies and saves, then tells the open Settings window to re-display the new font selections.

**Tech Stack:** C# / WPF (.NET Framework 4.8) shell, `Scratchpad.Core` (netstandard2.0) with xUnit tests (net10.0).

**Spec:** Agreed in chat on 2026-11-02 (small change, no separate design doc):
- Button labelled **Reset to defaults** at the bottom of the Settings window, below the Fonts group; window grows to fit.
- Click shows a Yes/No prompt owned by the Settings window: **"Reset fonts and font sizes to their defaults?"** No = nothing happens.
- Yes: editor/preview-text/preview-code font families → defaults (Consolas / preview built-in / preview built-in); editor and preview font sizes → default. Main window and the Settings window's pickers update immediately; both are saved to disk immediately.
- NOT touched: document text, window size/position, always-on-top, preview mode.

## Global Constraints

- Prompt text exactly: `Reset fonts and font sizes to their defaults?`; caption `Scratchpad`; button text exactly `Reset to defaults`.
- Defaults must come from `FontSizeStore` / `FontFamilyStore` — no duplicated literal values in the Shell.
- Window placement, always-on-top, and preview mode are not modified by a reset.
- Shell code can only be compiled in the sandbox (`EnableWindowsTargeting` is already on), not run.

## Review Focus

- Reset while the main window is in **preview mode**: preview must re-render with default fonts *and* the default zoom (both `ApplyPreviewFonts()` and `ApplyPreviewFontSize()`), and the editor size must also reset even though it isn't visible.
- Re-selecting combo items programmatically fires `SelectionChanged`; the Settings window must **not** raise `SettingsChanged` while it is re-displaying values (otherwise intermediate half-reset settings get applied and saved).
- Preview pickers whose default is `null` must show the `(default)` sentinel after reset, not a blank selection.
- Font sizes are normally only saved on window close/hide; a reset must save them **immediately** so a crash/kill afterwards doesn't lose it.
- Main window is always-on-top: the confirmation must be owned by the Settings window (`MessageBox.Show(this, …)`) so it can't appear behind.

---

### Task 1: Expose store defaults

**Files:**
- Modify: `Scratchpad.Core/FontSizeStore.cs`, `Scratchpad.Core/FontFamilyStore.cs`
- Test: `Scratchpad.Core.Tests/FontSizeStoreTests.cs`, `Scratchpad.Core.Tests/FontFamilyStoreTests.cs`

**Interfaces:**
- Produces: `public static FontSizeSettings FontSizeStore.Defaults { get; }` and `public static FontFamilySettings FontFamilyStore.Defaults { get; }` — return the same values `Load()` returns when the file is missing. (Both settings types are structs, so callers get a copy and can't mutate the shared default.)

- [ ] **Step 1: Write failing tests**

```csharp
// FontSizeStoreTests
[Fact]
public void Defaults_MatchesLoadWhenFileMissing()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
    Assert.Equal(new FontSizeStore(path).Load(), FontSizeStore.Defaults);
    Assert.Equal(Default, FontSizeStore.Defaults);
}

// FontFamilyStoreTests
[Fact]
public void Defaults_MatchesLoadWhenFileMissing()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
    Assert.Equal(new FontFamilyStore(path).Load(), FontFamilyStore.Defaults);
    Assert.True(FontFamilyStore.Defaults.IsDefault());
}
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test Scratchpad.sln` → compile error, `Defaults` not defined.
- [ ] **Step 3: Implement** — rename each private `DefaultSettings` field's use behind a public static `Defaults` property (keep one source of truth; `Load()` returns `Defaults` on failure).
- [ ] **Step 4: Run tests** — `dotnet test Scratchpad.sln` → all pass.
- [ ] **Step 5: Commit** — `feat: expose font store defaults`

### Task 2: Reset button in Settings window and reset handling in MainWindow

**Files:**
- Modify: `Scratchpad/SettingsWindow.xaml`, `Scratchpad/SettingsWindow.xaml.cs`, `Scratchpad/MainWindow.xaml.cs` (`SettingsButton_Click` and new handler near `OnFontFamilySettingsChanged`)

**Interfaces:**
- Consumes: `FontSizeStore.Defaults`, `FontFamilyStore.Defaults` (Task 1).
- Produces (SettingsWindow):
  - `public event Action ResetRequested;` — raised only after the user clicks Yes.
  - `public void ShowSettings(FontFamilySettings settings)` — sets the three combos' `SelectedItem` (same logic the constructor uses today; the constructor calls this method instead of duplicating it) **without** raising `SettingsChanged` (guard with a private `bool _isUpdatingSelection` checked in `RaiseSettingsChanged`).

- [ ] **Step 1: SettingsWindow XAML** — wrap the existing `GroupBox` in a `DockPanel`; add `<Button x:Name="ResetButton" DockPanel.Dock="Bottom" HorizontalAlignment="Right" Content="Reset to defaults" Click="ResetButton_Click" Padding="8,4" Margin="8,0,8,8" />` before the GroupBox; raise window `Height` from 220 to 260.
- [ ] **Step 2: SettingsWindow code** — add `ShowSettings` (extracted from constructor), the `_isUpdatingSelection` guard, `ResetRequested`, and `ResetButton_Click`: `MessageBox.Show(this, "Reset fonts and font sizes to their defaults?", "Scratchpad", MessageBoxButton.YesNo, MessageBoxImage.Question)`; on `MessageBoxResult.Yes` raise `ResetRequested`.
- [ ] **Step 3: MainWindow** — in `SettingsButton_Click` subscribe `settingsWindow.ResetRequested += OnResetRequested;`. Implement `private void OnResetRequested()`:
  - `_fontSizeSettings = FontSizeStore.Defaults; ApplyEditorFontSize(); ApplyPreviewFontSize(); _fontSizeStore.Save(_fontSizeSettings);`
  - `_fontFamilySettings = FontFamilyStore.Defaults; ApplyEditorFontFamily(); if (PreviewToggleButton.IsChecked == true) ApplyPreviewFonts(); _fontFamilyStore.Save(_fontFamilySettings);`
  - `_settingsWindow?.ShowSettings(_fontFamilySettings);`
- [ ] **Step 4: Build + tests** — `dotnet build Scratchpad.sln` → 0 warnings, 0 errors; `dotnet test Scratchpad.sln --no-build` → all pass.
- [ ] **Step 5: Commit** — `feat: add reset to defaults in Settings`
