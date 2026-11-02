# Preview Double-Click and Keep-Your-Place Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Switching between editor and preview keeps your place in both directions, and double-clicking the preview opens the editor at the clicked word.

**Architecture:**
- `Scratchpad.Core` gets `PreviewSourceMap`. It parses the note with the preview's Markdig pipeline into an ordered list of "units": the blocks Markdig.Wpf draws as one top-level-ish WPF block (a `Paragraph`, or a `Table`). Each unit carries its source character range.
- The Shell walks `PreviewViewer.Document` in the same order to get the matching WPF blocks. The two lists pair up by index. If the counts differ, it falls back to today's behaviour (nothing moves).
- The Shell measures and scrolls with WPF APIs. The pairing, the editor-offset → unit lookup, and the clicked-text → source-offset lookup are pure and unit tested.

**Tech Stack:**
- C# with WPF on .NET Framework 4.8 (Shell, compile-only in the sandbox).
- netstandard2.0 Core with Markdig 0.22.0.
- xUnit tests on net10.0.

**Spec:** `docs/superpowers/specs/2026-11-02-preview-double-click-design.md`

## Global Constraints

- No new NuGet dependencies; WPF/.NET Framework 4.8; Markdig.Wpf 0.5.0.1 and Markdig 0.22.0 as already referenced.
- Core must parse with `new MarkdownPipelineBuilder().UseTaskLists().Build()`, the same pipeline as `MainWindow` line 46 and `TaskListToggler`.
- Any failure (mismatched counts, layout not ready, unexpected element) → leave the views where they are; never throw out of a switch or a double-click.
- A double-click whose `OriginalSource` is a `CheckBox` or inside one → not handled, no switch.
- After any switch to the editor, `EditorTextBox` has keyboard focus.

## How Markdig.Wpf maps blocks (from the decompiled 0.5.0.1 renderers)

| Markdig block | WPF output |
|---|---|
| `ParagraphBlock`, `HeadingBlock`, `CodeBlock` (incl. fenced), `ThematicBreakBlock` | one `Paragraph` each |
| `QuoteBlock` | `Section` holding its children |
| `ListBlock` / `ListItemBlock` | `List` / `ListItem` holding children |
| `Table` (only if pipe tables were enabled — they aren't today) | one `Table` (treat as one unit; don't descend) |
| `HtmlBlock`, `LinkReferenceDefinition`, anything else | no renderer → nothing drawn |

So the **units** are:
- **Core:** a depth-first walk that emits `ParagraphBlock`, `HeadingBlock`, `CodeBlock`, `ThematicBreakBlock` and `Table` (without descending into it). It descends into every other `ContainerBlock` and skips every other leaf.
- **Shell:** a depth-first walk of `FlowDocument.Blocks` that emits `Paragraph` and `Table`, descends into `Section` (its `.Blocks`) and `List` (each `ListItem`'s `.Blocks`), and ignores anything else.

## Review Focus

1. CRLF line endings (the note is edited on Windows): the source offsets must index into the `\r\n` text correctly. This is pinned by a Core test.
2. Blank lines between blocks: an editor top line that sits on a blank line maps to the *next* block, not the previous one. Pinned by a Core test.
3. Link reference definitions and HTML blocks draw nothing. They must not shift the pairing. Pinned by a Core test (they produce no unit).
4. Double-click on empty space below the last paragraph: switch, caret at the end of the note, no exception. Handled in Task 3.
5. Empty note: the map is empty. Every Shell path must treat `index < 0` / empty lists as "don't move". Pinned by a Core test and guarded in Tasks 2 and 3.

---

### Task 1: Core source map

**Files:**
- Create: `Scratchpad.Core/PreviewSourceMap.cs`
- Test: `Scratchpad.Core.Tests/PreviewSourceMapTests.cs`

**Interfaces:**
- Produces:
  - `public struct SourceBlock { public SourceBlock(int start, int end); public int Start { get; } public int End { get; } }`. `End` is exclusive, i.e. Markdig `Span.End + 1`.
  - `public static class PreviewSourceMap` with three methods:
    - `IReadOnlyList<SourceBlock> Build(string markdown)`. A null or empty input gives an empty list.
    - `int BlockIndexAt(IReadOnlyList<SourceBlock> blocks, int offset)`. Returns the index of the first block whose `End > offset`; if there is none, the last index; if the list is empty, `-1`.
    - `int LocateInBlock(string markdown, SourceBlock block, string runText, int offsetInRun)`:
      - Search for `runText` (ordinal) within `markdown[block.Start, block.End)`.
      - If found: first match + `offsetInRun`, clamped to `block.End`.
      - If not found, or `runText` is null or empty: `block.Start`.

- [ ] **Step 1: Write the failing tests** in `PreviewSourceMapTests` (same style as `TaskListTogglerTests`: plain class, `[Fact]`, Arrange/Act/Assert with blank lines):
  - `Build_EmptyText_ReturnsNoBlocks`: `""` and `null` → `Count == 0`.
  - `Build_ParagraphsHeadingsCodeAndRule_OneBlockEach`: `"# Title\n\nPara one\n\n```\ncode\n```\n\n---\n"` → 4 blocks. `markdown.Substring(b[0].Start, b[0].End - b[0].Start) == "# Title"`, and `b[1]` starts at `markdown.IndexOf("Para one")`.
  - `Build_ListItemsAndQuotes_YieldTheirInnerParagraphs`: `"- [ ] alpha\n- beta\n\n> quoted\n"` → 3 blocks, starting at `IndexOf("[ ] alpha")`, `IndexOf("beta")`, `IndexOf("quoted")`.
  - `Build_LinkReferenceDefinitionsAndHtml_ProduceNoBlock`: `"Para\n\n[id]: http://x\n\n<div>hi</div>\n\nAfter\n"` → 2 blocks (`Para`, `After`).
  - `Build_CrLfText_OffsetsIndexTheOriginalString`: `"One\r\n\r\nTwo\r\n"` → `b[1].Start == markdown.IndexOf("Two")`.
  - `BlockIndexAt_OffsetInsideBlock_ReturnsThatBlock`, `BlockIndexAt_OffsetOnBlankLineBetweenBlocks_ReturnsNextBlock`, `BlockIndexAt_OffsetPastEnd_ReturnsLastBlock`, `BlockIndexAt_NoBlocks_ReturnsMinusOne`.
  - `LocateInBlock_FindsRunTextAndAddsOffset`: block for `"Some **bold** words"`, run `"words"`, offset 2 → `IndexOf("words") + 2`.
  - `LocateInBlock_TextNotInBlock_ReturnsBlockStart`: run `"zzz"` → `block.Start`. Also an empty run → `block.Start`.
  - `LocateInBlock_SearchesOnlyWithinTheBlock`: `"alpha\n\nbeta alpha\n"`, second block, run `"alpha"` → `LastIndexOf("alpha")`.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test Scratchpad.Core.Tests --filter PreviewSourceMapTests`
  Expected: build fails because `PreviewSourceMap` does not exist.

- [ ] **Step 3: Implement `PreviewSourceMap` and `SourceBlock`** in `Scratchpad.Core/PreviewSourceMap.cs`.
  - Use a private static pipeline like `TaskListToggler`'s.
  - Do the walk recursively over `ContainerBlock`, following the unit rules above. `Table` is `Markdig.Extensions.Tables.Table`.
  - XML doc comments in the same tone as `TaskListToggler`. Say why the unit rules mirror Markdig.Wpf's renderers.

- [ ] **Step 4: Run them to verify they pass.**
  Run: `dotnet test Scratchpad.Core.Tests`
  Expected: all pass (79 existing plus the new ones).

- [ ] **Step 5: Commit.** `feat: add preview source map`

### Task 2: Keep your place on Preview button / Ctrl+E

**Files:**
- Create: `Scratchpad/PreviewNavigator.cs`. This holds the WPF measuring and scrolling for the preview, to keep `MainWindow.xaml.cs` (771 lines) from growing much.
- Modify: `Scratchpad/MainWindow.xaml.cs`. Change `SetPreviewMode` (around line 148), add editor helpers, and construct the navigator in the constructor.

**Interfaces:**
- Consumes: `PreviewSourceMap.Build`, `PreviewSourceMap.BlockIndexAt`, `SourceBlock` (Task 1).
- Produces (`internal sealed class PreviewNavigator`, ctor `(ScrollViewer scrollViewer, MarkdownViewer viewer)`):
  - `IReadOnlyList<Block> GetUnits()`. The Shell-side walk of `viewer.Document`, following the unit rules above. Returns an empty list if `Document` is null.
  - `int TopUnitIndex(IReadOnlyList<Block> units)`. Returns the first unit whose bottom (`ContentEnd` rect bottom, in extent coordinates) is greater than `scrollViewer.VerticalOffset + 1`, or `-1`.
  - `int UnitIndexAtExtentY(IReadOnlyList<Block> units, double y)`. Same rule, with `y` in place of the offset; returns `-1` if `y` is below every unit.
  - `void ScrollUnitToTop(Block unit)`.
  - `Point? ToExtent(Point pointInViewer)`, `Rect? CharacterRectInExtent(TextPointer p)`, and `Visual RenderScope`. Task 3 uses these.

Measuring (an algorithm the signatures don't determine):
- `TextPointer.GetCharacterRect` returns a rect relative to the document's render scope, which is the internal `FlowDocumentView` inside the `FlowDocumentScrollViewer` template.
- Find it with a `VisualTreeHelper` depth-first search under `viewer` for the first visual whose `GetType().Name == "FlowDocumentView"`. Do the search lazily and cache it; re-find it if it is no longer a descendant.
- Convert to extent coordinates with `renderScope.TransformToAncestor(viewer)`. `viewer` is the `ScrollViewer`'s content, so its coordinates equal extent coordinates.
- `ScrollUnitToTop` = `scrollViewer.ScrollToVerticalOffset(top of unit.ContentStart rect in extent)`.
- If the render scope isn't found, or a rect is `Rect.Empty`, return `-1`/`null` and do nothing.
- Wrap each public method body in `try { … } catch (InvalidOperationException) { return fallback; }`. `TransformToAncestor` throws when the visuals aren't connected.

`MainWindow` changes:
- `private int EditorTopCharacterIndex()`:
  - `GetFirstVisibleLineIndex()`; if `< 0`, return `-1`.
  - Otherwise return `GetCharacterIndexFromLineIndex(line)`.
- `private void ScrollEditorToCharacter(int index)`:
  - `EditorTextBox.UpdateLayout()`, then `var rect = GetRectFromCharacterIndex(index)`.
  - If the rect isn't empty: `ScrollToVerticalOffset(EditorTextBox.VerticalOffset + rect.Top)`.
  - Otherwise: `ScrollToLine(GetLineIndexFromCharacterIndex(index))`.
- `SetPreviewMode(bool showPreview)` keeps its existing visibility logic and adds the following.
  - **To preview:**
    1. Before hiding the editor, if the editor is currently visible, capture `top = EditorTopCharacterIndex()`.
    2. `RenderPreview()`, swap the visibility, then `PreviewScrollViewer.UpdateLayout()`.
    3. If `top >= 0`: `map = Build(EditorTextBox.Text)`, `units = navigator.GetUnits()`.
    4. If `units.Count == map.Count` and `i = BlockIndexAt(map, top)` is `>= 0`: `navigator.ScrollUnitToTop(units[i])`.
  - **To editor:**
    1. Before hiding the preview, if the preview is currently visible: `units`, `map`, `i = navigator.TopUnitIndex(units)`.
    2. Swap the visibility, then `EditorTextBox.Focus()`.
    3. If the counts match and `i >= 0`: `EditorTextBox.CaretIndex = map[i].Start; ScrollEditorToCharacter(map[i].Start)`.
  - Factor the "to editor at a given caret" part into `private void SwitchToEditorAt(int? caretIndex)`, for Task 3 to reuse.
  - Wrap the place-keeping in `try { … } catch (Exception) { }` with a comment. That way a measuring failure degrades to today's behaviour; the visibility swap must still happen. This is in line with the project's "harden Shell paths that can't be run here" lesson.

- [ ] **Step 1: Implement `PreviewNavigator`** as specified above.
- [ ] **Step 2: Wire `SetPreviewMode`, `SwitchToEditorAt` and the editor helpers** in `MainWindow`.
- [ ] **Step 3: Build the Shell.**
  Run: `dotnet build Scratchpad/Scratchpad.csproj`
  Expected: `Build succeeded`, 0 errors.
- [ ] **Step 4: Run the Core suite** to make sure nothing regressed.
  Run: `dotnet test Scratchpad.Core.Tests`
  Expected: all pass.
- [ ] **Step 5: Commit.** `feat: keep scroll position when switching editor and preview`

### Task 3: Double-click the preview to edit there

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`. Add `MouseDoubleClick="PreviewScrollViewer_MouseDoubleClick"` to `PreviewScrollViewer`.
- Modify: `Scratchpad/MainWindow.xaml.cs`
- Modify: `Scratchpad/PreviewNavigator.cs`

**Interfaces:**
- Consumes:
  - From Task 2: `PreviewNavigator.GetUnits`, `UnitIndexAtExtentY`, `ToExtent`, `CharacterRectInExtent`, `RenderScope`, and `MainWindow.SwitchToEditorAt(int?)`.
  - From Task 1: `PreviewSourceMap.Build` and `LocateInBlock`.
- Produces: `PreviewNavigator.OffsetInRun(Run run, Point pointInExtent) → int`.
  - Walk `i = 0..run.Text.Length-1`.
  - For each `i`, take the rect of `run.ContentStart.GetPositionAtOffset(i)`.
  - Among the characters whose rect spans `pointInExtent.Y` vertically, return the last one whose `rect.Left <= pointInExtent.X`.
  - If none qualify: `0`.

`PreviewScrollViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)`:
1. **Checkbox check.** Walk up from `e.OriginalSource`:
   - For a `DependencyObject` that is a `Visual`, use `VisualTreeHelper.GetParent`.
   - For a `ContentElement`, use `LogicalTreeHelper.GetParent`.

   If a `CheckBox` is met → `return`, without setting `Handled`.
2. `e.Handled = true`. Then `units`, `map = Build(EditorTextBox.Text)`. If `units.Count != map.Count` → `SwitchToEditorAt(null)`, which falls back to the plain switch (place kept per Task 2 if possible).
3. **Find the clicked unit.**
   - Walk up the logical parents of `e.OriginalSource` (when it's a `ContentElement`) to the first ancestor that is in `units`. Use `IndexOf`.
   - If none is found: `y = navigator.ToExtent(e.GetPosition(PreviewViewer))?.Y`, then `i = UnitIndexAtExtentY(units, y)`.
   - If `i < 0` and the units are non-empty → the click is below the last unit. Caret = `EditorTextBox.Text.Length`.
4. **Caret within the unit.**
   - If `e.OriginalSource is Run run` inside unit `i`: `caret = LocateInBlock(text, map[i], run.Text, navigator.OffsetInRun(run, extentPoint))`.
   - Otherwise: `caret = map[i].Start`.
5. **Switch.** `SwitchToEditorAt(caret)`. This scrolls the caret's block to the top, which keeps the reading position.
6. **Wrap the body** in try/catch (Exception) → `SwitchToEditorAt(null)`, so a double-click always at least switches.

- [ ] **Step 1: Add `OffsetInRun`** to `PreviewNavigator`.
- [ ] **Step 2: Add the XAML event and the handler** as specified above.
- [ ] **Step 3: Build the Shell.**
  Run: `dotnet build Scratchpad/Scratchpad.csproj`
  Expected: `Build succeeded`, 0 errors.
- [ ] **Step 4: Run the Core suite.**
  Run: `dotnet test Scratchpad.Core.Tests`
  Expected: all pass.
- [ ] **Step 5: Commit.** `feat: double-click the preview to edit at that spot`

---

## Manual test script (for the user on Windows)

1. Write a long note: headings, a task list, a code block, a quote, and `---`.
2. In the editor, scroll halfway down and press **Preview**. The same section should be at the top of the preview.
3. Scroll further down the preview and press **Preview** again. That section should be at the top of the editor, the cursor should be at its start, and typing should work immediately.
4. Repeat steps 2 and 3 with **Ctrl+E**.
5. In the preview, double-click a word in the middle of a paragraph. The editor should open with the cursor at (or next to) that word, without jumping to the top.
6. Double-click a task checkbox. It should toggle twice (a net no-op), and the view should stay in preview.
7. Double-click the empty space below the last paragraph. The editor should open with the cursor at the end.
8. Repeat in dark mode, and with an empty note (no crash).
