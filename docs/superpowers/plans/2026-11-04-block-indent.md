# Block Indent / Unindent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the editor, pressing Tab with a selection that spans several lines indents each of those lines by 4 spaces. Shift+Tab removes one level of indent from every line the selection or caret touches.

**Architecture:**
- In `Scratchpad.Core` (unit-tested): a pure `LineIndenter` takes the text plus the selection. It returns either nothing (no change) or a single block replacement and the new selection.
- In the Shell: `Window_PreviewKeyDown` intercepts Tab and Shift+Tab while the editor has focus. It applies the replacement with `SelectedText`, so the edit is one undo step and the scroll position stays put, then restores the selection.

**Tech Stack:**
- WPF on .NET Framework 4.8 (Shell, compile-only in the sandbox).
- netstandard2.0 Core.
- xUnit.

**Spec:** the description agreed in chat on 2026-11-04. It is a small change, so there's no spec document. It is reproduced here:

- **What it's for:**
  - Stripping extra leading indent from pasted code (the user's case).
  - Nesting and un-nesting list items.
- **Tab, selection spanning more than one line:**
  - Indents every line the selection touches by **4 spaces**, including lines it only partly covers.
  - Does not replace the selection.
  - The same lines stay selected, so Tab can be pressed repeatedly.
- **Shift+Tab (any selection, or just the caret):**
  - Removes one level from each touched line. A level is one leading tab, or up to 4 leading spaces.
  - Lines with less indent lose what they have. Lines with none are untouched.
- **Tab with no selection, or a selection within one line:** unchanged. It types a tab character, as today.
- **Undo:** each indent or unindent is one Ctrl+Z step.
- **Preview mode:** unaffected.

## Global Constraints

- No new NuGet dependencies.
- The indent unit is exactly 4 spaces (`"    "`). Unindent removes exactly one leading `\t`, or else up to 4 leading spaces.
- "Lines" are logical lines separated by `\n`. The TextBox's word-wrap rows don't count. Both `\r\n` and `\n` endings must work. Windows TextBox text is `\r\n`.

## Decisions Made Here

These are the plan author's calls, not the user's:

- **"Spans more than one line" means the selected text contains a `\n`.** For example, selecting one whole line *including* its line break (Shift+Down from column 0) counts as multi-line, so Tab indents that line instead of deleting it.
- **A selection ending exactly at the start of a line (just after a `\n`) does not touch that line.** This is the usual editor rule. Without it, a Shift+Down selection would also indent the line below.
- **Indent skips empty lines** (zero length, so no trailing-whitespace litter). Whitespace-only lines are indented like any other line.
- **Selection after a multi-line edit:**
  - It starts at the first touched line's start.
  - If the original selection ended at a line start (see the rule above), it ends at that same line start. Otherwise it ends at the end of the last touched line, before its line break.
  - In both cases, whole touched lines end up selected.
- **Selection after unindenting with no `\n` in the selection** (caret, or a selection within one line): each end moves left by the removed amount and is clamped to the line start. The text the caret sat in stays under the caret.
- **If unindent would change nothing,** `Outdent` returns `null`. The Shell still swallows Shift+Tab, so focus doesn't jump to another control, and it makes no edit, so there's no empty undo step.

## Review Focus

1. **CRLF text.** The line end used for the new selection must sit before `\r\n`, not between `\r` and `\n`. Pinned in Task 1 (`Indent_CrLf_SelectionEndsBeforeLineBreak`).
2. **Last line with no trailing newline.** It must be handled the same as any other line. Pinned in Task 1 (`Indent_LastLineWithoutNewline_IsIndented`).
3. **Caret inside the leading whitespace being removed** (e.g. caret at column 2 of `"      x"`). It must clamp to the line start, not go before it onto the previous line. Pinned in Task 1 (`Outdent_CaretInsideRemovedIndent_ClampsToLineStart`).
4. **Mixed indent.** Given `"\t    x"`, only the tab goes. Given `"  \tx"`, only the 2 spaces go. Pinned in Task 1 (`Outdent_RemovesOneTabOrUpToFourSpaces`).
5. **Tab/Shift+Tab when the editor isn't focused, or in preview.** They must keep their normal behavior, e.g. focus navigation in Settings, which is a separate window but shares nothing, and toolbar buttons are `Focusable="False"`. The Shell guard is `EditorTextBox.IsKeyboardFocused`. Checked by the reviewer and by manual step 6.

---

### Task 1: Core `LineIndenter`

**Files:**
- Create: `Scratchpad.Core/LineIndenter.cs`
- Test: `Scratchpad.Core.Tests/LineIndenterTests.cs`

**Interfaces (Produces):**
- In the same file:

  ```csharp
  public sealed class IndentEdit
  {
      public int ReplaceStart { get; }    // start of the first touched line
      public int ReplaceLength { get; }   // through the end of the last touched line (excluding its break)
      public string Replacement { get; }  // the re-indented block
      public int SelectionStart { get; }  // in the text *after* the edit
      public int SelectionLength { get; }
  }
  ```

  Use a constructor that takes all five values.
- `public static class LineIndenter`:
  - `public const string IndentUnit = "    ";`
  - `public static IndentEdit Indent(string text, int selectionStart, int selectionLength)`: returns `null` when the selection contains no `\n`. The caller then lets Tab type normally.
  - `public static IndentEdit Outdent(string text, int selectionStart, int selectionLength)`: returns `null` when no touched line has any indent to remove.

**Tests:** write them as a small helper. `Apply(text, edit)` returns the new text, built as `text.Substring(0, ReplaceStart) + Replacement + text.Substring(ReplaceStart + ReplaceLength)`. Assert on the resulting text and on the selected substring, so the tests read as before/after strings. Use `\n` unless stated.

- [ ] **Step 1: Write the failing tests.**
  - `Indent_SelectionWithinOneLine_ReturnsNull`: `"abc\ndef"`, selection (1,1).
  - `Indent_TwoPartiallySelectedLines_IndentsBoth`:
    - Input `"abc\ndef\nghi"`, selection from index 1 to index 5 (`"bc\nd"`).
    - Text becomes `"    abc\n    def\nghi"`.
    - Selected text is `"    abc\n    def"`.
  - `Indent_SelectionEndingAtLineStart_ExcludesThatLine`:
    - Input `"abc\ndef\nghi"`, selection (0,4), i.e. `"abc\n"`.
    - Text becomes `"    abc\ndef\nghi"`.
    - Selected text is `"    abc\n"`.
  - `Indent_SkipsEmptyLines`:
    - Input `"a\n\nb"`, selecting all.
    - Text becomes `"    a\n\n    b"`.
  - `Indent_CrLf_SelectionEndsBeforeLineBreak`:
    - Input `"ab\r\ncd\r\nef"`, selection (0,5).
    - Text becomes `"    ab\r\n    cd\r\nef"`.
    - Selected text is `"    ab\r\n    cd"`.
  - `Indent_LastLineWithoutNewline_IsIndented`:
    - Input `"a\nb"`, selection (0,3).
    - Text becomes `"    a\n    b"`.
  - `Outdent_RemovesOneTabOrUpToFourSpaces`:
    - Input `"\t    x\n  \ty\n      z\nw"`, selecting all.
    - Text becomes `"    x\n\ty\n  z\nw"`.
  - `Outdent_NothingToRemove_ReturnsNull`: `"a\nb"`, selecting all.
  - `Outdent_Caret_UnindentsCurrentLineAndKeepsCaretOnSameText`:
    - Input `"a\n        bc"`, caret at the index of `c`, length 0.
    - Text becomes `"a\n    bc"`.
    - `SelectionStart` is the new index of `c`, `SelectionLength` 0.
  - `Outdent_CaretInsideRemovedIndent_ClampsToLineStart`:
    - Input `"a\n      x"`, caret at index 4 (column 2).
    - Text becomes `"a\n  x"`.
    - `SelectionStart` is 2.
  - `Outdent_MultiLine_SelectsWholeTouchedLines`:
    - Input `"    abc\n    def"`, selection (5,6).
    - Text becomes `"abc\ndef"`.
    - Selected text is `"abc\ndef"`.
- [ ] **Step 2: Run the tests and check they fail to compile.**
  - Run: `dotnet test Scratchpad.Core.Tests --filter LineIndenterTests`
  - Expected: a build error, `LineIndenter` not found.
- [ ] **Step 3: Implement `LineIndenter` and `IndentEdit`** with the rules in "Decisions Made Here".
  - Approach: find the touched-line range as follows:
    - Start: the line start of `selectionStart`, i.e. `LastIndexOf('\n', selectionStart - 1) + 1`.
    - End: the selection end, stepped back one character when it sits at a line start and the selection is non-empty. Then advance to the next `\n`, stepping back over a preceding `\r`.
  - Split that block on `\n`, re-indent each piece (a piece may end in `\r`; treat it as part of the line's content), and join.
- [ ] **Step 4: Run the tests and check they pass.**
  - Run: `dotnet test Scratchpad.Core.Tests`
  - Expected: all pass, including the existing suite.
- [ ] **Step 5: Commit** with `feat: LineIndenter indents and unindents touched lines`.

### Task 2: Shell wiring

**Files:**
- Modify: `Scratchpad/MainWindow.xaml.cs`: `Window_PreviewKeyDown` (about line 1262), plus a new private `ApplyIndentEdit(IndentEdit edit)` next to `InsertDate`.

**Interfaces (Consumes):** `LineIndenter.Indent`, `LineIndenter.Outdent` and `IndentEdit` from Task 1.

- [ ] **Step 1: Add the key handling to `Window_PreviewKeyDown`.** Add two branches, both guarded by `EditorTextBox.IsKeyboardFocused && e.Key == Key.Tab`:
  - `Keyboard.Modifiers == ModifierKeys.None`:
    - Call `LineIndenter.Indent(EditorTextBox.Text, EditorTextBox.SelectionStart, EditorTextBox.SelectionLength)`.
    - If the result is non-null, apply it and set `e.Handled = true`.
    - If it's null, leave the event unhandled so the TextBox types its tab.
  - `Keyboard.Modifiers == ModifierKeys.Shift`:
    - Call `Outdent`, and apply the result if it's non-null.
    - Always set `e.Handled = true`.
  - Add a short comment explaining why Shift+Tab is always swallowed: so focus never jumps out of the note.
- [ ] **Step 2: Implement `ApplyIndentEdit`.**
  - Call `EditorTextBox.Select(edit.ReplaceStart, edit.ReplaceLength)`.
  - Assign `EditorTextBox.SelectedText = edit.Replacement`.
  - Then call `EditorTextBox.Select(edit.SelectionStart, edit.SelectionLength)`.
  - Add a one-line comment, in the style of `InsertDate`'s, saying that `SelectedText` keeps it one undo step and leaves the scroll position alone.
- [ ] **Step 3: Build the solution.**
  - Run: `dotnet build Scratchpad.sln`. `EnableWindowsTargeting` is already on.
  - Expected: `Build succeeded`, with 0 errors.
- [ ] **Step 4: Commit** with `feat: Tab/Shift+Tab indent and unindent selected lines`.

## Manual test on Windows (for the user)

1. Select parts of 3 lines and press Tab. All 3 lines gain 4 spaces and stay selected. Press Tab again and they gain 4 more.
2. Press Shift+Tab twice. The lines are back where they started. Press Ctrl+Z, and one step comes back per press.
3. Paste code indented by 8 spaces or by tabs, select it, and press Shift+Tab. One level comes off each line. Lines already at the margin don't move.
4. Put the caret mid-line on an indented list item and press Shift+Tab. That line unindents, and the caret stays on the same character.
5. With no selection, press Tab. It still types a tab character.
6. In the Settings window, Tab and Shift+Tab still move between controls. In preview mode, Tab does nothing to the text.
