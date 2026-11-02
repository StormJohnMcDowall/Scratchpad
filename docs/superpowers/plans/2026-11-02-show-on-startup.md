# Show Window on Startup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A Settings checkbox, "Show the window when Scratchpad starts", that makes every start (including Windows sign-in) open the main window instead of starting hidden in the tray.

**Architecture:** A Core `StartupWindowStore` persists `StartupWindowSettings { bool ShowWindowOnStartup }` (default false) like the other stores. `MainWindow` loads it in its constructor and exposes `ShowWindowOnStartup`; `App.OnStartup` calls `mainWindow.ShowAndActivate()` as its last step when it is true. The Settings window gains a "Startup" group with the checkbox; Reset to defaults unticks it.

**Spec:** agreed in chat 2026-11-02 (small change, no design doc):
- New "Startup" group between Appearance and Fonts; checkbox text exactly `Show the window when Scratchpad starts`.
- Unticked (default) = today's behaviour; ticked = every start shows the window (normal placement restore, tray icon as usual).
- Persisted (`%LOCALAPPDATA%\Scratchpad\startup-window.json`, best-effort, unreadable → false). Reset to defaults unticks it. Toggling takes effect at next start only.
- Second-launch behaviour unchanged.

## Global Constraints
- Same best-effort atomic load/save pattern and `Defaults` property as `ThemeStore`.
- Startup failure to read the setting must never stop the app starting (falls back to hidden).

## Review Focus
- Dark mode: the Settings checkbox must be readable (system CheckBox sets its own dark Foreground — needs a dark implicit CheckBox style that does not affect the preview's task-list checkboxes, which carry an explicit style).
- Showing at startup must happen after the tray icon and single-instance listener exist, and must restore the saved placement (Window_Loaded runs on first Show).
- Reset with Settings open re-ticks/unticks the checkbox without raising its changed event.

---

### Task 1: StartupWindowStore (Core, TDD)
**Files:** Create `Scratchpad.Core/StartupWindowSettings.cs`, `Scratchpad.Core/StartupWindowStore.cs`; Test `Scratchpad.Core.Tests/StartupWindowStoreTests.cs`.
**Produces:** `struct StartupWindowSettings : IEquatable<…> { bool ShowWindowOnStartup }`; `class StartupWindowStore` with `static Defaults` (false), `static DefaultFilePath`, `Load()`, `Save()`.
- [ ] Tests: `Load_ReturnsDefaultWhenFileMissing`, `Load_ReturnsDefaultWhenFileIsCorrupt`, `SaveThenLoad_RoundTripsTrue`, `Defaults_IsHidden` (false). Run → compile errors.
- [ ] Implement; run `dotnet test Scratchpad.sln` → all pass. Commit `feat: add show-on-startup persistence`.

### Task 2: Settings checkbox, startup show, reset (Shell; build-verified)
**Files:** `Scratchpad/SettingsWindow.xaml(.cs)`, `Scratchpad/MainWindow.xaml.cs`, `Scratchpad/App.xaml.cs`, `Scratchpad/Themes/Dark.xaml`.
**Produces (SettingsWindow):** constructor gains `bool showWindowOnStartup`; `event Action<bool> ShowWindowOnStartupChanged` (guarded by `_isUpdatingSelection`); `ShowSettings(FontFamilySettings, ThemePreference, bool showWindowOnStartup)`.
- [ ] Settings XAML: `GroupBox Header="Startup"` (DockPanel.Dock="Top") after Appearance with `CheckBox x:Name="ShowOnStartupCheckBox"`; window Height grows to fit.
- [ ] MainWindow: `_startupWindowStore`/`_startupWindowSettings` loaded in constructor; `public bool ShowWindowOnStartup`; handler saves on change; `OnResetRequested` resets to `StartupWindowStore.Defaults`, saves, and passes it to `ShowSettings`.
- [ ] App.OnStartup: last statement `if (mainWindow.ShowWindowOnStartup) mainWindow.ShowAndActivate();`.
- [ ] Dark.xaml: implicit `CheckBox` style BasedOn `PostItTaskListCheckBoxStyle` with `Margin` reset to `0`.
- [ ] `dotnet build Scratchpad.sln` → 0 warnings/errors; tests pass. Commit `feat: add show window on startup setting`.
