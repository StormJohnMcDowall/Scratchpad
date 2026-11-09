# Wrap Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Typing `*`, `_`, `` ` `` or `~` while text is selected in the editor wraps the selection in that character (and keeps the original text selected) instead of replacing it.

**Architecture:** A pure Core function `SelectionWrapper.Wrap` decides the edit and returns the existing `IndentEdit` (replace a range, then select a range). The Shell catches the typed character with `EditorTextBox.PreviewTextInput` (a character, not a physical key, so it works on every layout and with numpad `*`) and applies the edit through the existing `ApplyIndentEdit`, which already makes it one undo step that restores the original selection.

**Tech Stack:** C#, Scratchpad.Core (netstandard2.0) + xUnit tests (net10.0), WPF shell (net48, compiles in the sandbox with `EnableWindowsTargeting`).

**Spec:** Agreed in chat on 2026-11-09 (small change, no design doc). Summary:
- Selection + type one of `*` `_` `` ` `` `~` → `<c>selection<c>`, the original text stays selected, so typing `*` again gives `**text**`, and again `***text***`.
- Nothing selected → the character types as it does today.
- Ctrl+Z undoes the wrap in one step and restores the original selection.
- Non-goals: no auto-pairing with no selection, no brackets/quotes, no unwrap, editor only (not Settings boxes).

## Global Constraints

- Wrap characters are exactly `*`, `_`, `` ` ``, `~`.
- Core stays netstandard2.0 with no new dependencies.
- `IndentEdit` and `ApplyIndentEdit` are reused as they are (my call: renaming them to something generic isn't worth the churn for one more caller).

## Review Focus

1. **Selection ending in a line break** (Shift+Down, or Shift+End past a line end): `*abc\n*` is broken markdown. Expected: a trailing `\n` or `\r\n` is left outside the wrap, and the selection covers just the wrapped text. My call. Pinned in Task 1.
2. **Selection that is only a line break**: there's nothing to wrap once the break is excluded. Expected: return null, and the character types normally (replacing the selection), as it does today. Pinned in Task 1.
3. **Multi-character `TextInput`** (an IME commit, a dead key producing two characters, a paste-like input): expected no wrap unless the text is exactly one wrap character. Pinned in Task 1.
4. **The preview is showing**: the editor is collapsed, so it gets no text input. No code is needed, but check that the handler is on `EditorTextBox`, not on the window.
5. **The "before big delete" backup**: a wrap only adds characters, so it must never trigger it. `ApplyIndentEdit`'s BeginChange/EndChange raises a single TextChanged with a longer text. Check this by reading the code; there's no test.

---

### Task 1: Core `SelectionWrapper`

**Files:**
- Create: `Scratchpad.Core/SelectionWrapper.cs`
- Test: `Scratchpad.Core.Tests/SelectionWrapperTests.cs`

**Interfaces:**
- Consumes: `IndentEdit(int replaceStart, int replaceLength, string replacement, int selectionStart, int selectionLength)` from `Scratchpad.Core/LineIndenter.cs`.
- Produces: `public static class SelectionWrapper` with `public static IndentEdit Wrap(string text, int selectionStart, int selectionLength, string typed)`. It returns null when the caller should let the character type normally.

- [ ] **Step 1: Write the failing tests** in `SelectionWrapperTests.cs`, with the same `Apply`/`Selected` helpers as `LineIndenterTests`:

```csharp
[Theory]
[InlineData("*")] [InlineData("_")] [InlineData("`")] [InlineData("~")]
public void Wrap_SelectionAndWrapChar_WrapsAndKeepsTextSelected(string c)
{
    var text = "say hello now";
    var edit = SelectionWrapper.Wrap(text, 4, 5, c);
    Assert.Equal($"say {c}hello{c} now", Apply(text, edit));
    Assert.Equal("hello", Selected(text, edit));
}

[Fact]
public void Wrap_TwiceWithStar_MakesBold()
{
    var text = "hello";
    var first = SelectionWrapper.Wrap(text, 0, 5, "*");
    var once = Apply(text, first);
    var second = SelectionWrapper.Wrap(once, first.SelectionStart, first.SelectionLength, "*");
    Assert.Equal("**hello**", Apply(once, second));
    Assert.Equal("hello", Selected(once, second));
}

[Fact] public void Wrap_NoSelection_ReturnsNull() => Assert.Null(SelectionWrapper.Wrap("abc", 1, 0, "*"));
[Theory] [InlineData("a")] [InlineData("(")] [InlineData("**")] [InlineData("")] [InlineData(null)]
public void Wrap_NotASingleWrapChar_ReturnsNull(string typed) => Assert.Null(SelectionWrapper.Wrap("abc", 0, 3, typed));

[Theory] [InlineData("abc\ndef", 0, 4)] [InlineData("abc\r\ndef", 0, 5)]
public void Wrap_TrailingLineBreak_StaysOutside(string text, int start, int length)
{
    var edit = SelectionWrapper.Wrap(text, start, length, "*");
    Assert.StartsWith("*abc*\n", Apply(text, edit).Replace("\r\n", "\n"));
    Assert.Equal("abc", Selected(text, edit));
}

[Theory] [InlineData("abc\ndef", 3, 1)] [InlineData("abc\r\ndef", 3, 2)]
public void Wrap_OnlyALineBreak_ReturnsNull(string text, int start, int length) => Assert.Null(SelectionWrapper.Wrap(text, start, length, "*"));

[Fact]
public void Wrap_MultiLineSelection_WrapsWhole()
{
    var text = "ab\ncd";
    var edit = SelectionWrapper.Wrap(text, 1, 3, "_");
    Assert.Equal("a_b\nc_d", Apply(text, edit));
    Assert.Equal("b\nc", Selected(text, edit));
}
```

- [ ] **Step 2: Run the tests and check they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter SelectionWrapperTests`
Expected: the build fails with "The name 'SelectionWrapper' does not exist".

- [ ] **Step 3: Implement `SelectionWrapper.Wrap`**. Keep a `WrapCharacters = "*_`~"` constant. Trim one trailing `\n` (and a `\r` before it) off the selection. If nothing is left, return null. Otherwise replace the trimmed range with `c + inner + c` and select `start + 1` for `inner.Length`.

- [ ] **Step 4: Run the tests and check they pass**

Run: `dotnet test Scratchpad.Core.Tests`
Expected: all pass, including the existing suites.

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/SelectionWrapper.cs Scratchpad.Core.Tests/SelectionWrapperTests.cs
git commit -m "feat: core rule for wrapping a selection in markdown characters"
```

### Task 2: Wire it into the editor

**Files:**
- Modify: `Scratchpad/MainWindow.xaml:104-113` (add `PreviewTextInput="EditorTextBox_PreviewTextInput"`)
- Modify: `Scratchpad/MainWindow.xaml.cs` (new handler next to `ApplyIndentEdit`, ~line 1169)

**Interfaces:**
- Consumes: `SelectionWrapper.Wrap` (Task 1) and `ApplyIndentEdit(IndentEdit)` (existing).

- [ ] **Step 1: Add the handler** `private void EditorTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)`. Call `SelectionWrapper.Wrap(EditorTextBox.Text, EditorTextBox.SelectionStart, EditorTextBox.SelectionLength, e.Text)`. If the result isn't null, call `ApplyIndentEdit(edit)` and set `e.Handled = true`. Add a short comment on why it uses PreviewTextInput rather than PreviewKeyDown (characters vs physical keys). Update the `ApplyIndentEdit` comment so it no longer reads as indent-only.

- [ ] **Step 2: Build the shell**

Run: `dotnet build Scratchpad.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: typing * _ \` ~ over a selection wraps it"
```

**Manual checks on Windows** (they can't be run in the sandbox):
1. Select a word and type `*`: you get `*word*`, with `word` still selected.
2. Type `*` twice more: `***word***`.
3. Try `_`, `` ` `` and `~`, and numpad `*`.
4. Ctrl+Z once: back to the plain word, with the word selected.
5. With nothing selected, `*` types one star.
6. Select a line with Shift+Down and type `*`: the stars sit around the text, not the line break.
