# Settings Panel & Font Family Choice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a general-purpose Settings window (opened from a new toolbar button) with a "Fonts" section letting the user independently choose the editor's font, the preview's normal-text font, and the preview's code font, from fonts actually installed on the machine — persisted across restarts.

**Architecture:** Persistence (`FontFamilySettings`/`FontFamilyStore`) lives in `Scratchpad.Core`, mirroring `FontSizeSettings`/`FontSizeStore` exactly, and is fully unit-tested in this sandbox. Font enumeration and monospace/symbol-font detection are inherently WPF/Windows-only (no equivalent API in `netstandard2.0`), so that logic — and the Settings window itself, and all wiring into `MainWindow` — lives entirely in the `Scratchpad` (WPF) project, compiled here (`EnableWindowsTargeting` is set) but verified by build + manual checklist, same as every other Shell task in this codebase. Font-family application to the preview reuses this codebase's established technique (seen in `WireTaskListCheckBoxes`) of walking the rendered `FlowDocument`'s blocks/inlines directly after render and stamping `FontFamily` as a local value — never by trying to override Markdig.Wpf's internal `ComponentResourceKey` styles, which would require guessing their exact `TargetType` with no way to verify it in this sandbox (a wrong guess throws at render time).

**Tech Stack:** C#, same projects as the rest of the app — `Scratchpad.Core` (`netstandard2.0`) for persistence, `Scratchpad.Core.Tests` (`net10.0`, xUnit) for its tests, `Scratchpad` (WPF, `net48`) for font detection, the new Settings window, and `MainWindow` wiring.

**Spec:** [docs/superpowers/specs/2026-10-29-settings-font-family-design.md](../specs/2026-10-29-settings-font-family-design.md)

## Global Constraints

- Three independent, persisted font-family choices: editor, preview normal text, preview code (both fenced blocks and inline spans).
- Font lists are detected from the machine's actually-installed fonts at runtime — never a hardcoded name.
- Editor and code pickers are limited to monospace/fixed-pitch fonts; the normal-text picker excludes symbol/dingbat fonts only.
- The preview's normal-text choice applies to headings as well as paragraphs; the code choice applies to inline code spans as well as fenced blocks.
- The Settings window is general-purpose (not "Font Settings"), non-modal, with no OK/Cancel/Apply — changes apply immediately, consistent with the rest of the app having no explicit save step.
- A user who never opens Settings sees no visual change: the editor still renders in `Consolas` (today's hardcoded value) and the preview still renders with Markdig.Wpf's own built-in defaults, untouched, until a choice is explicitly made.
- `Scratchpad.Core` stays free of WPF/Windows-only dependencies — anything touching installed fonts or WPF font APIs lives in the `Scratchpad` project.

## Review Focus

- A hand-edited settings file with an empty/whitespace `EditorFontFamily` must fall back to the built-in default (`"Consolas"`) — an empty string later reaching `new FontFamily("")` in the Shell throws `ArgumentException` and would crash the app on startup (Task 1: `FontFamilyStore`).
- A missing or unparseable settings file must fall back to defaults without throwing, mirroring every other store in this app (Task 1: `FontFamilyStore`).
- A persisted font family that is no longer installed (uninstalled since last run, or a settings file carried to another machine) must be treated as unset/default rather than crash or silently substitute something unexpected — validated against the live installed-font list at load time (Task 4: WPF-only, no automated test possible in this sandbox; pin the exact fallback behavior and flag for careful review).
- The editor's, preview-normal's, and preview-code's settings must never cross-apply to the wrong control or the wrong preview element type — WPF-only, no automated test possible; the exact field-to-element mapping is pinned explicitly in Task 4 and should get close attention in review, the same class of risk flagged (and found clean) in the prior font-sizing plan.
- The monospace-detection heuristic must not throw when a font's glyph table lacks the comparison characters (some symbol/foreign-script fonts won't have ASCII glyphs at all) — must use a non-throwing lookup and treat "can't determine" as "not monospace" rather than crashing the whole font-enumeration pass, which would break the Settings window entirely (Task 2: WPF-only, no automated test; pin the exact method body).

---

## Task 1: Font Family Persistence

**Files:**
- Create: `Scratchpad.Core/FontFamilySettings.cs`
- Create: `Scratchpad.Core/FontFamilyStore.cs`
- Test: `Scratchpad.Core.Tests/FontFamilyStoreTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.FontFamilySettings` — a struct with `string EditorFontFamily { get; set; }`, `string PreviewNormalFontFamily { get; set; }`, `string PreviewCodeFontFamily { get; set; }`, implementing `IEquatable<FontFamilySettings>` with `==`/`!=` (ordinal string equality, treating two `null`s as equal). `Scratchpad.Core.FontFamilyStore` — `public const string DefaultEditorFontFamily = "Consolas"`, `public static string DefaultFilePath { get; }`, `public string FilePath { get; }`, `public FontFamilyStore(string filePath)`, `public FontFamilySettings Load()`, `public void Save(FontFamilySettings settings)`. Task 4 consumes all of this.

- [ ] **Step 1: Write the failing tests**

```csharp
public class FontFamilyStoreTests
{
    static readonly FontFamilySettings Default = new FontFamilySettings
    {
        EditorFontFamily = FontFamilyStore.DefaultEditorFontFamily,
        PreviewNormalFontFamily = null,
        PreviewCodeFontFamily = null
    };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        Assert.Equal(Default, new FontFamilyStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new FontFamilyStore(path).Load());
    }

    [Fact]
    public void Load_NormalizesEmptyEditorFontFamilyToDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontFamily\":\"\",\"PreviewNormalFontFamily\":null,\"PreviewCodeFontFamily\":null}");

        Assert.Equal(FontFamilyStore.DefaultEditorFontFamily, new FontFamilyStore(path).Load().EditorFontFamily);
    }

    [Fact]
    public void Load_NormalizesWhitespacePreviewFontFamilyToNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontFamily\":\"Consolas\",\"PreviewNormalFontFamily\":\"   \",\"PreviewCodeFontFamily\":null}");

        Assert.Null(new FontFamilyStore(path).Load().PreviewNormalFontFamily);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsIndependentValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        var store = new FontFamilyStore(path);
        var settings = new FontFamilySettings
        {
            EditorFontFamily = "Cascadia Mono",
            PreviewNormalFontFamily = "Georgia",
            PreviewCodeFontFamily = "Courier New"
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNullPreviewFields()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        var store = new FontFamilyStore(path);

        store.Save(Default);

        Assert.Equal(Default, store.Load());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter FontFamilyStoreTests`
Expected: FAIL to compile — `FontFamilySettings`/`FontFamilyStore` do not exist.

- [ ] **Step 3: Implement `FontFamilySettings` and `FontFamilyStore`**

`FontFamilySettings`: three auto-properties as above; `Equals`/`GetHashCode`/operators via `string.Equals(a, b, StringComparison.Ordinal)` for each field (treats two `null`s, and `null` vs itself, as equal — unlike `WindowBounds`/`FontSizeSettings`, fields are reference types here, so delegate to `string.Equals` rather than `.Equals()` instance calls, which would throw on a `null` receiver).

`FontFamilyStore.DefaultFilePath`: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scratchpad", "font-family.json")`. `Load()`: read and deserialize as `FontFamilySettings`; on any exception, return `new FontFamilySettings { EditorFontFamily = DefaultEditorFontFamily, PreviewNormalFontFamily = null, PreviewCodeFontFamily = null }`; on success, normalize before returning — `string.IsNullOrWhiteSpace` on `EditorFontFamily` substitutes `DefaultEditorFontFamily`; `string.IsNullOrWhiteSpace` on either preview field substitutes `null`. `Save()`: same atomic write-temp-then-replace pattern as `WindowPlacementStore.Save`/`FontSizeStore.Save` (`Scratchpad.Core/FontSizeStore.cs:44-66`), wrapped the same way so any exception is swallowed.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter FontFamilyStoreTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/FontFamilySettings.cs Scratchpad.Core/FontFamilyStore.cs Scratchpad.Core.Tests/FontFamilyStoreTests.cs
git commit -m "feat: add font family persistence"
```

---

## Task 2: Installed Font Detection

*WPF-only (needs `System.Windows.Media.Fonts`/`GlyphTypeface`, unavailable in `netstandard2.0`), so this lives in the `Scratchpad` project. This sandbox can compile it but not run it — verified by build plus a manual check on the target Windows machine.*

**Files:**
- Create: `Scratchpad/FontCatalog.cs`

**Interfaces:**
- Produces: `Scratchpad.FontCatalog` — `public static IReadOnlyList<FontFamily> MonospaceFamilies { get; }`, `public static IReadOnlyList<FontFamily> NormalTextFamilies { get; }`. Tasks 3 and 4 consume both.

- [ ] **Step 1: Implement `FontCatalog`**

```csharp
public static class FontCatalog
{
    public static IReadOnlyList<FontFamily> MonospaceFamilies { get; } =
        Fonts.SystemFontFamilies.Where(IsMonospace).OrderBy(f => f.Source).ToList();

    public static IReadOnlyList<FontFamily> NormalTextFamilies { get; } =
        Fonts.SystemFontFamilies.Where(f => !IsSymbolFont(f)).OrderBy(f => f.Source).ToList();

    private static bool IsMonospace(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphTypeface))
        {
            return false;
        }

        // 'i' and 'm' have very different advance widths in any proportional font and
        // identical ones in a true monospace font. TryGetValue (not the indexer) because a
        // font missing either glyph (e.g. a symbol or non-Latin-only font) must be excluded,
        // not thrown from — one unmeasurable font must never break the whole catalog.
        if (!glyphTypeface.CharacterToGlyphMap.TryGetValue('i', out var narrowGlyph) ||
            !glyphTypeface.CharacterToGlyphMap.TryGetValue('m', out var wideGlyph))
        {
            return false;
        }

        return Math.Abs(glyphTypeface.AdvanceWidths[narrowGlyph] - glyphTypeface.AdvanceWidths[wideGlyph]) < 0.001;
    }

    private static bool IsSymbolFont(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out var glyphTypeface) && glyphTypeface.Symbol;
    }
}
```

A font whose glyph typeface can't be resolved at all (`TryGetGlyphTypeface` returns `false`) is excluded from `MonospaceFamilies` but kept in `NormalTextFamilies` (erring toward inclusion there, since the normal-text list isn't trying to isolate a special category the way the monospace list is).

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Manually verify, on the target Windows machine**

Add a temporary `Console.WriteLine(string.Join(", ", FontCatalog.MonospaceFamilies))` (or inspect via debugger) and confirm: known monospace fonts on the machine (e.g. `Consolas`, `Courier New`, `Lucida Console`) appear in `MonospaceFamilies`; known proportional fonts (e.g. `Segoe UI`, `Calibri`) do not; `Wingdings`/`Webdings` do not appear in `NormalTextFamilies`. Remove the temporary line afterward.

- [ ] **Step 4: Commit**

```bash
git add Scratchpad/FontCatalog.cs
git commit -m "feat: add installed monospace/normal-text font detection"
```

---

## Task 3: Settings Window

*WPF-only; build-verify plus manual check, same as Task 2.*

**Files:**
- Create: `Scratchpad/SettingsWindow.xaml`
- Create: `Scratchpad/SettingsWindow.xaml.cs`

**Interfaces:**
- Consumes: `Scratchpad.Core.FontFamilySettings` (Task 1); `FontCatalog.MonospaceFamilies`/`NormalTextFamilies` (Task 2).
- Produces: `Scratchpad.SettingsWindow` — `public SettingsWindow(FontFamilySettings current, IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)`; `public event Action<FontFamilySettings> SettingsChanged`. Task 4 consumes both.

- [ ] **Step 1: Build `SettingsWindow.xaml`**

A plain `Window` (`Title="Settings"`, modest fixed size, e.g. `Width="360" Height="220"`) containing a `GroupBox Header="Fonts"` with three labelled rows, each a `Label` beside a `ComboBox` (`x:Name="EditorFontComboBox"`, `x:Name="PreviewNormalFontComboBox"`, `x:Name="PreviewCodeFontComboBox"`) — "Editor", "Preview text", "Preview code". Each `ComboBox`'s `DisplayMemberPath="Source"` (so it shows each `FontFamily`'s name rather than its `ToString()`).

- [ ] **Step 2: Implement `SettingsWindow.xaml.cs`**

```csharp
public partial class SettingsWindow : Window
{
    public event Action<FontFamilySettings> SettingsChanged;

    public SettingsWindow(FontFamilySettings current, IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)
    {
        InitializeComponent();

        EditorFontComboBox.ItemsSource = monospaceFamilies;
        PreviewCodeFontComboBox.ItemsSource = monospaceFamilies;
        PreviewNormalFontComboBox.ItemsSource = normalTextFamilies;

        EditorFontComboBox.SelectedItem = monospaceFamilies.FirstOrDefault(f => f.Source == current.EditorFontFamily);
        PreviewNormalFontComboBox.SelectedItem = normalTextFamilies.FirstOrDefault(f => f.Source == current.PreviewNormalFontFamily);
        PreviewCodeFontComboBox.SelectedItem = monospaceFamilies.FirstOrDefault(f => f.Source == current.PreviewCodeFontFamily);

        EditorFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
        PreviewNormalFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
        PreviewCodeFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
    }

    private void RaiseSettingsChanged()
    {
        SettingsChanged?.Invoke(new FontFamilySettings
        {
            EditorFontFamily = (EditorFontComboBox.SelectedItem as FontFamily)?.Source,
            PreviewNormalFontFamily = (PreviewNormalFontComboBox.SelectedItem as FontFamily)?.Source,
            PreviewCodeFontFamily = (PreviewCodeFontComboBox.SelectedItem as FontFamily)?.Source
        });
    }
}
```

`FirstOrDefault` on a `null` `current.*FontFamily` (the two preview fields, by default) returns `null` — WPF's `ComboBox.SelectedItem = null` leaves the box with no selection, which is correct: no override is configured. No `OK`/`Cancel`/`Apply` controls — this window only ever closes via its own title-bar close button, consistent with the design's "changes apply live" decision.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Scratchpad/SettingsWindow.xaml Scratchpad/SettingsWindow.xaml.cs
git commit -m "feat: add Settings window with font pickers"
```

---

## Task 4: Wire Settings Into MainWindow

*WPF-only; build-verify plus manual check, same as Tasks 2-3. This task also carries the Review Focus items for "font no longer installed" and "fields never cross-apply" — read both before writing the code.*

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `FontFamilySettings`, `FontFamilyStore.DefaultFilePath/Load()/Save()` (Task 1); `FontCatalog.MonospaceFamilies/NormalTextFamilies` (Task 2); `SettingsWindow` (Task 3).

- [ ] **Step 1: Add the toolbar button and drop the hardcoded editor font**

In the top `StackPanel`, after the existing zoom buttons, add `<Button x:Name="SettingsButton" Content="Settings" Click="SettingsButton_Click" Padding="8,4" Margin="2,4" Focusable="False" />` (non-focusable for the same reason the zoom buttons are — clicking it must not steal keyboard focus from the editor). Remove `FontFamily="Consolas"` from `EditorTextBox` — its font now comes entirely from code-behind.

- [ ] **Step 2: Load, validate, and apply font settings in `MainWindow`'s constructor**

Add fields:

```csharp
private readonly FontFamilyStore _fontFamilyStore = new FontFamilyStore(FontFamilyStore.DefaultFilePath);
private FontFamilySettings _fontFamilySettings;
```

In the constructor, after the existing font-size loading (`MainWindow.xaml.cs`, added by the prior plan — load font-family settings the same way, in the constructor, not `Window_Loaded`, for the same reason: this app always starts hidden in the tray, so `Window_Loaded` never runs on the common exit-without-opening path):

```csharp
_fontFamilySettings = ValidateAgainstInstalledFonts(_fontFamilyStore.Load());
ApplyEditorFontFamily();
```

Implement `ValidateAgainstInstalledFonts(FontFamilySettings settings)`: a persisted font family is only trustworthy if it's still installed — `FontFamilyStore.Load()` already guarantees non-empty/non-whitespace strings (Task 1), but can't check installation status itself (`Scratchpad.Core` has no font API). Check each field against `FontCatalog.MonospaceFamilies`/`NormalTextFamilies` (by `.Source`); substitute `FontFamilyStore.DefaultEditorFontFamily` for a missing `EditorFontFamily`, and `null` for either missing preview field.

- [ ] **Step 3: Apply fonts to the editor and the rendered preview**

```csharp
private void ApplyEditorFontFamily()
{
    EditorTextBox.FontFamily = new FontFamily(_fontFamilySettings.EditorFontFamily);
}

private void ApplyPreviewFonts()
{
    if (PreviewViewer.Document == null)
    {
        return;
    }

    ApplyPreviewFonts(PreviewViewer.Document.Blocks);
}

private void ApplyPreviewFonts(IEnumerable<Block> blocks)
{
    var codeBlockStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeBlockStyleKey) as Style;
    var codeStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeStyleKey) as Style;

    foreach (var block in blocks)
    {
        if (block is Paragraph paragraph)
        {
            var isCodeBlock = codeBlockStyle != null && ReferenceEquals(paragraph.Style, codeBlockStyle);
            var paragraphFontFamily = isCodeBlock ? _fontFamilySettings.PreviewCodeFontFamily : _fontFamilySettings.PreviewNormalFontFamily;
            if (paragraphFontFamily != null)
            {
                paragraph.FontFamily = new FontFamily(paragraphFontFamily);
            }

            if (!isCodeBlock && _fontFamilySettings.PreviewCodeFontFamily != null && codeStyle != null)
            {
                foreach (var run in paragraph.Inlines.OfType<Run>())
                {
                    if (ReferenceEquals(run.Style, codeStyle))
                    {
                        run.FontFamily = new FontFamily(_fontFamilySettings.PreviewCodeFontFamily);
                    }
                }
            }
        }
        else if (block is Section section)
        {
            ApplyPreviewFonts(section.Blocks);
        }
        else if (block is List list)
        {
            foreach (var item in list.ListItems)
            {
                ApplyPreviewFonts(item.Blocks);
            }
        }
    }
}
```

A `null` `_fontFamilySettings.Preview*FontFamily` means "no override" — leave that element's `FontFamily` untouched, so Markdig.Wpf's own default renders exactly as it does today for anyone who's never opened Settings. This mirrors `FindTaskListCheckBoxes`'s recursion shape (`Section`/`List`/`ListItem`) rather than inventing a new tree-walk pattern.

Call `ApplyPreviewFonts()` inside `RenderPreview()`, after the existing `ApplyPreviewFontSize()` call.

- [ ] **Step 4: Open Settings and react to changes**

```csharp
private void SettingsButton_Click(object sender, RoutedEventArgs e)
{
    var settingsWindow = new SettingsWindow(_fontFamilySettings, FontCatalog.MonospaceFamilies, FontCatalog.NormalTextFamilies);
    settingsWindow.SettingsChanged += OnFontFamilySettingsChanged;
    settingsWindow.Show();
}

private void OnFontFamilySettingsChanged(FontFamilySettings settings)
{
    _fontFamilySettings = settings;
    ApplyEditorFontFamily();
    if (PreviewToggleButton.IsChecked == true)
    {
        ApplyPreviewFonts();
    }
    _fontFamilyStore.Save(_fontFamilySettings);
}
```

Saved immediately on every change (not deferred to `Window_Closing`, unlike font size) — a combo-box selection is one deliberate, infrequent action, not a rapid-fire gesture, so there's no debouncing concern, and it means the setting survives even if `Window_Closing` never runs for an unrelated reason. `settingsWindow` is intentionally not tracked for reuse or disposal — `Show()` is non-modal and the window is simple enough that opening a second one (if the user clicks the toolbar button again) is harmless; closing either is independent of `MainWindow`.

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Manually verify, on the target Windows machine**

- Launch the app with no settings file present — editor renders in Consolas, preview renders with Markdig.Wpf's own default look, exactly as before this feature existed.
- Open Settings (toolbar button); pick a different editor font — the editor updates immediately, with keyboard focus still usable in it afterward (clicking the button didn't steal focus).
- With the preview showing, pick a different "Preview text" font — headings and paragraphs both update; pick a different "Preview code" font — fenced code blocks and inline `code spans` both update, independently of the text font.
- Close the app, reopen it — all three choices are restored.
- Edit `%LOCALAPPDATA%\Scratchpad\font-family.json` to name a font that isn't installed, relaunch — the app starts normally, that field falls back to its default instead of crashing or showing a blank combo-box selection.
- Exit from the tray without ever opening the main window (same scenario the prior font-sizing review caught as a data-loss bug) — relaunch and open Settings — the previously-chosen fonts are still there, not reset to default.

- [ ] **Step 7: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: wire font family settings into MainWindow"
```
