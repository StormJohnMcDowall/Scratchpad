# Font Sizing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor and the rendered preview each be resized independently (bigger/smaller font), via toolbar buttons, keyboard shortcuts, and Ctrl+scroll-wheel, with the chosen sizes persisted across restarts.

**Architecture:** The clamping/stepping math and the persisted settings live in `Scratchpad.Core` (testable in this sandbox), following the existing `WindowPlacement`/`WindowPlacementStore` split exactly. The WPF shell (`Scratchpad` project) wires three input surfaces — toolbar buttons, keyboard shortcuts, Ctrl+scroll — to the same two `Core`-backed operations, applying the result to whichever view (editor or preview) is currently visible. Shell tasks compile in this sandbox (`EnableWindowsTargeting` is already set on `Scratchpad.csproj`) but cannot run, so each shell task ends in a manual verification checklist for the target Windows machine, same as the rest of this app's Shell-side work.

**Tech Stack:** C#, same projects as the rest of the app — `Scratchpad.Core` (`netstandard2.0`) for logic/persistence, `Scratchpad.Core.Tests` (`net10.0`, xUnit) for its tests, `Scratchpad` (WPF, `net48`) for the UI.

**Spec:** No separate design doc — this feature was brainstormed directly in chat (first increment of a small feature roadmap; see `docs/superpowers/specs/2026-10-29-scratchpad-design.md` for the app's overall design). The agreed requirements are captured in Global Constraints below.

## Global Constraints

- The editor and the preview each have their own font size, persisted independently — not a single shared size.
- One set of controls (buttons, shortcuts, Ctrl+scroll) acts on whichever view is currently visible, since editor/preview is a toggle, not a split view.
- Explicit +/- buttons are required in the toolbar; keyboard shortcuts and Ctrl+scroll-wheel zoom are additional, not substitutes.
- Reset-to-default is keyboard-only (no visible reset button), consistent with the compact toolbar layout chosen during brainstorming.
- Sizes persist across restarts using the same pattern as `WindowPlacementStore`: atomic JSON write (temp file + replace) to a file under `%LOCALAPPDATA%\Scratchpad`, with load/save that never throws.

## Review Focus

- A hand-edited or corrupted-but-still-valid-JSON settings file with an out-of-range or non-positive size (e.g. `0`, `-5`, `9999`) must be clamped into the supported range before it ever reaches a WPF `FontSize` property — WPF throws for `FontSize <= 0`, so an unclamped load would crash the app on startup (Task 2: `FontSizeStore`).
- A missing or unparseable settings file must fall back to default sizes without throwing, mirroring `WindowPlacementStore` (Task 2: `FontSizeStore`).
- Repeatedly zooming in or out past the supported range (via button, shortcut, or scroll) must clamp at the min/max rather than overshoot (Task 1: `FontSizing`).
- The window being closed without ever having been shown (e.g. started minimized to the tray and exited from the tray menu, so `Window_Loaded` never ran) must not persist the in-memory settings field's zero-value default — it must already hold sane default sizes at that point (Task 3: field initializer, same bug class `WindowPlacementStore`'s NaN guard exists to prevent).
- The editor's and preview's sizes must never cross-apply to the wrong control — this can't be unit-tested (it's WPF-only code this sandbox can't run), so the exact field-to-control mapping is pinned explicitly in Task 3 and should get close attention in review.

---

## Task 1: Font Size Clamping & Stepping

**Files:**
- Create: `Scratchpad.Core/FontSizing.cs`
- Test: `Scratchpad.Core.Tests/FontSizingTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.FontSizing` — `public const double Default = 14`, `Min = 8`, `Max = 36`, `Step = 2`; `public static double Clamp(double value)`; `public static double Increase(double current)`; `public static double Decrease(double current)`. Tasks 2 and 3 consume all of these.

- [ ] **Step 1: Write the failing tests**

```csharp
public class FontSizingTests
{
    [Fact]
    public void Increase_AddsOneStep() =>
        Assert.Equal(16, FontSizing.Increase(14));

    [Fact]
    public void Increase_ClampsAtMax() =>
        Assert.Equal(FontSizing.Max, FontSizing.Increase(FontSizing.Max - 1));

    [Fact]
    public void Decrease_SubtractsOneStep() =>
        Assert.Equal(12, FontSizing.Decrease(14));

    [Fact]
    public void Decrease_ClampsAtMin() =>
        Assert.Equal(FontSizing.Min, FontSizing.Decrease(FontSizing.Min + 1));

    [Theory]
    [InlineData(9999, 36)]
    [InlineData(-5, 8)]
    [InlineData(0, 8)]
    [InlineData(20, 20)]
    public void Clamp_KeepsValueWithinRange(double value, double expected) =>
        Assert.Equal(expected, FontSizing.Clamp(value));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter FontSizingTests`
Expected: FAIL to compile — `FontSizing` does not exist.

- [ ] **Step 3: Implement `FontSizing` in `Scratchpad.Core/FontSizing.cs`**

`Clamp(value) => Math.Max(Min, Math.Min(Max, value))`. `Increase(current) => Clamp(current + Step)`. `Decrease(current) => Clamp(current - Step)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter FontSizingTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/FontSizing.cs Scratchpad.Core.Tests/FontSizingTests.cs
git commit -m "feat: add font size clamping and stepping logic"
```

---

## Task 2: Font Size Persistence

**Files:**
- Create: `Scratchpad.Core/FontSizeSettings.cs`
- Create: `Scratchpad.Core/FontSizeStore.cs`
- Test: `Scratchpad.Core.Tests/FontSizeStoreTests.cs`

**Interfaces:**
- Consumes: `FontSizing.Default`, `FontSizing.Clamp` (Task 1).
- Produces: `Scratchpad.Core.FontSizeSettings` — a struct (mirroring `WindowBounds`) with `double EditorFontSize { get; set; }`, `double PreviewFontSize { get; set; }`, implementing `IEquatable<FontSizeSettings>` with `==`/`!=`. `Scratchpad.Core.FontSizeStore` — `public static string DefaultFilePath { get; }`, `public string FilePath { get; }`, `public FontSizeStore(string filePath)`, `public FontSizeSettings Load()`, `public void Save(FontSizeSettings settings)`. Task 3 consumes all of this.

- [ ] **Step 1: Write the failing tests**

```csharp
public class FontSizeStoreTests
{
    static readonly FontSizeSettings Default = new FontSizeSettings
    {
        EditorFontSize = FontSizing.Default,
        PreviewFontSize = FontSizing.Default
    };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        Assert.Equal(Default, new FontSizeStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new FontSizeStore(path).Load());
    }

    [Fact]
    public void Load_ClampsOutOfRangeStoredValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontSize\":9999,\"PreviewFontSize\":-5}");

        var result = new FontSizeStore(path).Load();

        Assert.Equal(FontSizing.Max, result.EditorFontSize);
        Assert.Equal(FontSizing.Min, result.PreviewFontSize);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsIndependentValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        var store = new FontSizeStore(path);
        var settings = new FontSizeSettings { EditorFontSize = 20, PreviewFontSize = 10 };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Save_WithNaNValues_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        var store = new FontSizeStore(path);
        var settings = new FontSizeSettings { EditorFontSize = double.NaN, PreviewFontSize = double.NaN };

        var ex = Record.Exception(() => store.Save(settings));

        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter FontSizeStoreTests`
Expected: FAIL to compile — `FontSizeSettings`/`FontSizeStore` do not exist.

- [ ] **Step 3: Implement `FontSizeSettings` and `FontSizeStore`**

`FontSizeSettings`: copy `WindowBounds`'s `Equals`/`GetHashCode`/operator pattern (`Scratchpad.Core/WindowPlacement.cs:6-39`) with the two `double` properties above instead of four.

`FontSizeStore.DefaultFilePath`: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scratchpad", "font-size.json")`. `Load()`: read and deserialize as `FontSizeSettings`; on any exception, return `new FontSizeSettings { EditorFontSize = FontSizing.Default, PreviewFontSize = FontSizing.Default }`; on success, return the deserialized value with both fields passed through `FontSizing.Clamp`. `Save()`: same atomic write-temp-then-replace pattern as `WindowPlacementStore.Save` (`Scratchpad.Core/WindowPlacementStore.cs:41-70`), wrapped the same way so any exception is swallowed.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter FontSizeStoreTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/FontSizeSettings.cs Scratchpad.Core/FontSizeStore.cs Scratchpad.Core.Tests/FontSizeStoreTests.cs
git commit -m "feat: add font size persistence"
```

---

## Task 3: Toolbar Buttons & Lifecycle Wiring

*This task wires already-tested `Scratchpad.Core` units into the WPF shell. The shell only builds on Windows-targeted tooling; this sandbox can compile it (`EnableWindowsTargeting` is set) but not run it, so verification is a build check here plus a manual checklist for the target Windows machine.*

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `FontSizing.Default/Increase/Decrease` (Task 1); `FontSizeSettings`, `FontSizeStore.DefaultFilePath/Load()/Save()` (Task 2).
- Produces: `MainWindow.AdjustFontSize(int direction)` and `MainWindow.ResetFontSize()` (private) — Tasks 4 and 5 call both.

- [ ] **Step 1: Add the toolbar controls**

In the top `StackPanel` (`Scratchpad/MainWindow.xaml:9-12`), after `AlwaysOnTopToggleButton`, add a thin vertical divider and two buttons:

```xml
<Border Width="1" Margin="4,6" Background="#FFBEBEBE" />
<Button x:Name="ZoomOutButton" Content="-" Click="ZoomOutButton_Click" Padding="8,4" Margin="2,4" MinWidth="28" />
<Button x:Name="ZoomInButton" Content="+" Click="ZoomInButton_Click" Padding="8,4" Margin="2,4" MinWidth="28" />
```

- [ ] **Step 2: Wire lifecycle and the active-view adjustment methods in `MainWindow.xaml.cs`**

Add fields:

```csharp
private readonly FontSizeStore _fontSizeStore = new FontSizeStore(FontSizeStore.DefaultFilePath);
// Initialized to real defaults (not the struct's zero-value) so that if the window is
// closed without ever being shown — e.g. exited from the tray before Window_Loaded ever
// runs — Closing still persists usable sizes instead of 0pt fonts. Same bug class as the
// NaN guard on window placement.
private FontSizeSettings _fontSizeSettings = new FontSizeSettings
{
    EditorFontSize = FontSizing.Default,
    PreviewFontSize = FontSizing.Default
};
```

In `Window_Loaded` (`MainWindow.xaml.cs:30-40`), after loading the document, add `_fontSizeSettings = _fontSizeStore.Load(); EditorTextBox.FontSize = _fontSizeSettings.EditorFontSize;`.

In `RenderPreview` (`MainWindow.xaml.cs:94-98`), after `PreviewViewer.Markdown = EditorTextBox.Text;`, add `PreviewViewer.Document.FontSize = _fontSizeSettings.PreviewFontSize;` (before `WireTaskListCheckBoxes()`).

Add:

```csharp
private void AdjustFontSize(int direction)
{
    if (PreviewToggleButton.IsChecked == true)
    {
        _fontSizeSettings.PreviewFontSize = direction > 0
            ? FontSizing.Increase(_fontSizeSettings.PreviewFontSize)
            : FontSizing.Decrease(_fontSizeSettings.PreviewFontSize);
        PreviewViewer.Document.FontSize = _fontSizeSettings.PreviewFontSize;
    }
    else
    {
        _fontSizeSettings.EditorFontSize = direction > 0
            ? FontSizing.Increase(_fontSizeSettings.EditorFontSize)
            : FontSizing.Decrease(_fontSizeSettings.EditorFontSize);
        EditorTextBox.FontSize = _fontSizeSettings.EditorFontSize;
    }
}

private void ResetFontSize()
{
    if (PreviewToggleButton.IsChecked == true)
    {
        _fontSizeSettings.PreviewFontSize = FontSizing.Default;
        PreviewViewer.Document.FontSize = FontSizing.Default;
    }
    else
    {
        _fontSizeSettings.EditorFontSize = FontSizing.Default;
        EditorTextBox.FontSize = FontSizing.Default;
    }
}

private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-1);
private void ZoomInButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(1);
```

`PreviewViewer.Document` is safe to dereference whenever `PreviewToggleButton.IsChecked == true`: the only two places that set it true (`PreviewToggleButton_Click`, the Ctrl+E branch in `Window_PreviewKeyDown`) both call `RenderPreview()` first via `SetPreviewMode`.

In `Window_Closing` (`MainWindow.xaml.cs:175-210`), add `_fontSizeStore.Save(_fontSizeSettings);` alongside the existing `_placementStore.Save(bounds)` call — no NaN-style guard is needed here since `FontSizeSettings` values only ever come from `FontSizing`'s clamped operations or its own `Default`.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manually verify, on the target Windows machine**

- Launch the app; click `+` a few times in editor mode — editor text grows. Click `-` past the floor — it stops shrinking rather than erroring.
- Toggle to preview (Ctrl+E or the button) — preview text is still at its own last size, not the editor's.
- Click `+`/`-` in preview mode — only the preview resizes; toggle back to editor — its size is unchanged from before.
- Close and relaunch the app — both sizes are restored as left.
- Edit `%LOCALAPPDATA%\Scratchpad\font-size.json` to contain an invalid value (e.g. `0` or garbage text) and relaunch — the app starts normally at a sane size instead of crashing.

- [ ] **Step 5: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: add toolbar font size controls"
```

---

## Task 4: Keyboard Shortcuts

**Files:**
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainWindow.AdjustFontSize(int)`, `MainWindow.ResetFontSize()` (Task 3).

- [ ] **Step 1: Add shortcut handling to `Window_PreviewKeyDown`**

In `Window_PreviewKeyDown` (`MainWindow.xaml.cs:160-173`), add branches alongside the existing Ctrl+E/Ctrl+T ones, each setting `e.Handled = true`:

- Ctrl + (`Key.OemPlus` or `Key.Add`) → `AdjustFontSize(1)`
- Ctrl + (`Key.OemMinus` or `Key.Subtract`) → `AdjustFontSize(-1)`
- Ctrl + (`Key.D0` or `Key.NumPad0`) → `ResetFontSize()`

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Manually verify, on the target Windows machine**

- In editor mode: Ctrl+= (and Ctrl+Numpad+) grows the text; Ctrl+- (and Ctrl+Numpad-) shrinks it; Ctrl+0 (and Ctrl+Numpad0) resets it to default.
- Switch to preview mode and repeat all three — only the preview is affected.
- Confirm Ctrl+E (preview toggle) and Ctrl+T (always-on-top) still work unchanged.

- [ ] **Step 4: Commit**

```bash
git add Scratchpad/MainWindow.xaml.cs
git commit -m "feat: add keyboard shortcuts for font size"
```

---

## Task 5: Ctrl+Scroll-Wheel Zoom

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainWindow.AdjustFontSize(int)` (Task 3).

- [ ] **Step 1: Wire a window-level preview mouse-wheel handler**

Add `PreviewMouseWheel="Window_PreviewMouseWheel"` to the `Window` element (`Scratchpad/MainWindow.xaml:1-7`, alongside the existing `PreviewKeyDown`).

- [ ] **Step 2: Implement the handler in `MainWindow.xaml.cs`**

```csharp
private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
{
    if (Keyboard.Modifiers == ModifierKeys.Control)
    {
        AdjustFontSize(Math.Sign(e.Delta));
        e.Handled = true;
    }
}
```

Only when Ctrl is held is the event marked handled — an un-modified scroll must keep working for normal text scrolling in both the editor and the preview.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manually verify, on the target Windows machine**

- In editor mode, hold Ctrl and scroll up over the text — it grows; scroll down — it shrinks, stopping at the floor/ceiling.
- Release Ctrl and scroll — the editor scrolls its content normally, with no size change.
- Repeat both checks in preview mode.

- [ ] **Step 5: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: add Ctrl+scroll-wheel font zoom"
```
