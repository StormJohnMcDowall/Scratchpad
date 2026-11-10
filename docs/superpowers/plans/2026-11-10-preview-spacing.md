# Preview Heading Spacing and Task-List Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the rendered preview, headings sit close to their content, and lists made only of tasks show a checkbox without a bullet.

**Architecture:** One more post-render pass in `MainWindow.RenderPreview`, next to `ApplyPreviewFonts`/`ApplyPreviewTheme`. It stamps local values onto Markdig.Wpf's FlowDocument: a `Margin` on heading paragraphs (found by style reference, as existing passes do) and `MarkerStyle`/`Padding` on all-task `List`s. The numbers and the "is this an all-task list" rule live in Core so they're unit-tested; the Shell pass only walks the tree.

**Tech Stack:** C#, WPF (net48), Markdig.Wpf 0.5.0.1, xUnit.

**Spec:** No design doc (approved as a small change in chat, 2026-11-10). The approved description:
- Headings: moderate gap above, small gap below, proportional to each heading's own font size. Heading font sizes unchanged (user kept H1's size).
- Double marker: a WPF `List` whose **every** item is a task loses its bullets and its checkboxes move left to roughly where the bullets were. Mixed lists keep bullets. Nested lists are judged one list at a time.
- Unchanged: editor, markdown text, checkbox clicking, double-click-to-edit, scroll keeping, both themes, backups.

## Global Constraints

- Preview only; the editor and the note's text never change.
- Light and dark must both get the change (it is layout, not colour).
- Must be idempotent: Markdig.Wpf keeps the same FlowDocument when the text is unchanged, so re-showing the preview re-runs the pass on already-stamped elements.
- Shell (WPF) code can only be compiled in the sandbox (`EnableWindowsTargeting`), never run.

## Review Focus

1. A note whose first block is a heading: it must not get a gap above it at the top of the preview (Task 1 test `HeadingMargin_FirstBlock_HasNoTopGap`).
2. A list mixing tasks and plain items must keep its bullets (Task 1 test `IsTaskList_MixedItems_False`).
3. An empty list item or an item whose first block isn't a paragraph (e.g. a code block) is not a task (Task 1 tests `IsTaskList_ItemWithoutLeadingCheckbox_False`, `IsTaskList_NoItems_False`).
4. Checkbox clicks still toggle the right task after the change (manual Windows check 3; the pass only touches `Margin`/`MarkerStyle`/`Padding`, never inlines).
5. Double-click in the preview still lands on the clicked line after headings move (manual Windows check 4; `PreviewNavigator` measures live positions, so it should follow).

---

## File Structure

- Create `Scratchpad.Core/PreviewLayout.cs`: pure numbers and rules (no WPF types).
- Create `Scratchpad.Core.Tests/PreviewLayoutTests.cs`.
- Modify `Scratchpad/MainWindow.xaml.cs`: new `ApplyPreviewLayout()` called from `RenderPreview()`.

### Task 1: Core layout rules

**Files:**
- Create: `Scratchpad.Core/PreviewLayout.cs`
- Test: `Scratchpad.Core.Tests/PreviewLayoutTests.cs`

**Interfaces:**
- Produces:
  - `public struct VerticalMargin { public double Top; public double Bottom; }` (or a readonly struct with ctor + properties `Top`, `Bottom`)
  - `public static class PreviewLayout`
    - `public const double HeadingTopEm = 0.6;`
    - `public const double HeadingBottomEm = 0.15;`
    - `public const double TaskListIndentEm = 0.5;`
    - `public static VerticalMargin HeadingMargin(double fontSize, bool isFirstBlock)` — Top = `isFirstBlock ? 0 : fontSize * HeadingTopEm`, Bottom = `fontSize * HeadingBottomEm`.
    - `public static bool IsTaskList(IReadOnlyList<bool> itemStartsWithCheckbox)` — true only if the list is non-empty and every entry is true.
    - `public static double TaskListLeftPadding(double fontSize)` — `fontSize * TaskListIndentEm`.

- [ ] **Step 1: Write the failing tests** in `PreviewLayoutTests.cs` (same style as `FontSizingTests`):

```csharp
[Fact] public void HeadingMargin_ScalesWithFontSize()
{ var m = PreviewLayout.HeadingMargin(40, false); Assert.Equal(24, m.Top, 6); Assert.Equal(6, m.Bottom, 6); }
[Fact] public void HeadingMargin_FirstBlock_HasNoTopGap()
{ var m = PreviewLayout.HeadingMargin(40, true); Assert.Equal(0, m.Top); Assert.Equal(6, m.Bottom, 6); }
[Fact] public void HeadingMargin_BottomSmallerThanTop()
{ var m = PreviewLayout.HeadingMargin(20, false); Assert.True(m.Bottom < m.Top); }
[Fact] public void IsTaskList_AllItemsTasks_True() => Assert.True(PreviewLayout.IsTaskList(new[] { true, true }));
[Fact] public void IsTaskList_MixedItems_False() => Assert.False(PreviewLayout.IsTaskList(new[] { true, false, true }));
[Fact] public void IsTaskList_ItemWithoutLeadingCheckbox_False() => Assert.False(PreviewLayout.IsTaskList(new[] { false }));
[Fact] public void IsTaskList_NoItems_False() => Assert.False(PreviewLayout.IsTaskList(new bool[0]));
[Fact] public void TaskListLeftPadding_ScalesWithFontSize() => Assert.Equal(8, PreviewLayout.TaskListLeftPadding(16), 6);
```

- [ ] **Step 2: Run, expect compile failure**
Run: `dotnet test Scratchpad.Core.Tests --filter PreviewLayoutTests`
Expected: build error, `PreviewLayout` not found.

- [ ] **Step 3: Implement `PreviewLayout`** per the Interfaces block. `netstandard2.0`, so no `Array.Empty` assumptions in Core beyond what it supports; a header comment says why the values are em-based (preview zoom is a LayoutTransform, so em-based numbers stay in proportion for free).

- [ ] **Step 4: Run all Core tests**
Run: `dotnet test Scratchpad.Core.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**
`git commit -m "feat: core spacing rules for preview headings and task lists"`

### Task 2: Apply the rules in the preview

**Files:**
- Modify: `Scratchpad/MainWindow.xaml.cs` (`RenderPreview` ~line 565; new method near `ApplyPreviewBackground`)

**Interfaces:**
- Consumes: `PreviewLayout.HeadingMargin`, `PreviewLayout.IsTaskList`, `PreviewLayout.TaskListLeftPadding`.
- Produces: `private void ApplyPreviewLayout()` and `private void ApplyPreviewLayout(IEnumerable<Block> blocks, bool isTopLevel)`.

- [ ] **Step 1: Call it** from `RenderPreview()`, after `ApplyPreviewBackground()` and before `WireTaskListCheckBoxes()`. Return early when `PreviewViewer.Document == null`.

- [ ] **Step 2: Headings.** Collect the six heading styles once per pass via `PreviewViewer.TryFindResource(Markdig.Wpf.Styles.Heading1StyleKey … Heading6StyleKey) as Style` (skip nulls). For each `Paragraph` whose `Style` is reference-equal to one of them, set `paragraph.Margin = new Thickness(old.Left, m.Top, old.Right, m.Bottom)` with `m = PreviewLayout.HeadingMargin(paragraph.FontSize, isFirstBlock)`, where `isFirstBlock` is true only for the first block of the document's top-level `Blocks`. Keep `Left`/`Right` from the current margin, but if the current margin has `NaN` sides (WPF's "auto"), use 0 for them. Recurse into `Section.Blocks` and each `ListItem.Blocks` (headings can sit inside quotes/lists), with `isFirstBlock` false there.

- [ ] **Step 3: Task lists.** For each `List`, build the `bool` list: an item counts when its first block is a `Paragraph` whose first inline is an `InlineUIContainer` with a `CheckBox` child (the same "first inline" rule `TaskListToggler` counts by). If `PreviewLayout.IsTaskList(...)`, set `list.MarkerStyle = TextMarkerStyle.None` and `list.Padding = new Thickness(PreviewLayout.TaskListLeftPadding(list.FontSize), p.Top, p.Right, p.Bottom)` (keep other sides; NaN → 0). Recurse into each item's blocks either way, so nested lists are judged on their own. Idempotent by construction (values recomputed from font size, not from the previous padding).

- [ ] **Step 4: Comment** the method like its neighbours: why local values (Markdig.Wpf sets explicit styles, so implicit styles never reach these elements), why a first-block exception, why "first inline" matches the toggler.

- [ ] **Step 5: Build the Shell**
Run: `dotnet build Scratchpad.sln`
Expected: 0 errors, no new warnings.

- [ ] **Step 6: Run all Core tests**
Run: `dotnet test Scratchpad.Core.Tests`
Expected: all pass.

- [ ] **Step 7: Commit**
`git commit -m "feat: tighter heading spacing and no bullets on task lists in the preview"`

## Manual checks on Windows (for the user, after merge)

Using `screenshot-sample.md` pasted into the note:
1. Preview: "Tue 10 Nov" sits at the top with no big gap below; "Today" and "Notes from stand-up" have a clear gap above and sit close to their lists.
2. The "Today" list shows checkboxes only, no bullets, and they're left of where the bullets were; "Notes from stand-up" still has bullets.
3. Click each checkbox: the right task toggles, once.
4. Double-click a line under a heading: the editor opens at that line.
5. Ctrl+scroll zoom in/out: spacing stays in proportion.
6. Switch to dark (Settings → Theme): same spacing; checkboxes still styled.
7. A mixed list (`- [ ] a` / `- b`) keeps bullets on both.
