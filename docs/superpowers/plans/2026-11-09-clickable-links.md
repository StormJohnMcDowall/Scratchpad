# Clickable Links Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A single click on a link in the preview opens it in the default browser, and Ctrl+click on a markdown link in the editor does the same.

**Architecture:** Markdig.Wpf already gives every preview `Hyperlink` `Command = Markdig.Wpf.Commands.Hyperlink` and `CommandParameter = <url string>`. Nothing binds that command, so the links are disabled. The Shell adds a `CommandBinding` on the window, gated by a Core rule that decides which URLs may open. For the editor, a Core function finds the link under a character index by parsing with `NotePipeline`.

**Tech Stack:** C#, Scratchpad.Core (netstandard2.0, Markdig 0.22) + xUnit; WPF shell (net48, Markdig.Wpf 0.5.0.1).

**Spec:** Agreed in chat on 2026-11-09 (a small change, so there's no design doc):
- The user writes `[text](url)`. In the preview, a single click on a link opens it in the default browser, and the note stays where it is.
- Hovering a preview link shows a hand cursor and a tooltip with the full address.
- `<https://…>` autolinks work too.
- Bonus: Ctrl+click on a link in the editor opens it. Ctrl+Shift+click stays peek.
- Only `http://`, `https://` and `mailto:` open. Anything else is ignored. File and UNC links are not wanted for now (my call: the user didn't ask for them).

## Global Constraints

- Openable schemes are exactly `http`, `https` and `mailto` (case-insensitive), and the URL must be absolute.
- Launching never throws out of a handler. If there's no browser or the launch fails, nothing happens.
- Core stays netstandard2.0 with no new dependencies.

## Review Focus

1. **Double-clicking a link.** Every click runs the Hyperlink command, so a double-click would open the page twice and also jump to the editor. Expected: one launch, and no jump to the editor. Task 2 adds a guard (same URL within 500 ms is ignored) and has `PreviewScrollViewer_MouseDoubleClick` return early inside a `Hyperlink`.
2. **A link that isn't openable** (`[x](foo)`, `[x](file:///c:/x.exe)`, or Markdig.Wpf's `#` fallback for malformed URLs). Expected: it can't be clicked, keeps the link colour, and still shows its address on hover. `CanExecute` is false, so the Hyperlink is disabled. Set `ToolTipService.ShowOnDisabled`. Pinned in Task 1 by `IsOpenable` tests.
3. **A link with a title** (`[x](https://a "Title")`). Markdig.Wpf puts the title in the ToolTip. Expected: keep the title, since the user wrote it, and set the URL as the tooltip only when there isn't one.
4. **Ctrl+click in the editor that isn't on a link.** Expected: the TextBox behaves as normal (caret moves); handle the click only when a link opened.
5. **Ctrl+click on the link text versus the `(url)` part.** Expected: anywhere from `[` to `)` counts. Pinned in Task 1.

---

### Task 1: Core `NoteLinks`

**Files:**
- Create: `Scratchpad.Core/NoteLinks.cs`
- Test: `Scratchpad.Core.Tests/NoteLinksTests.cs`

**Interfaces:**
- Consumes: `NotePipeline.Create()`.
- Produces: `public static class NoteLinks` with:
  - `public static bool IsOpenable(string url)`
  - `public static string UrlAt(string markdown, int index)`: the URL of the link (`LinkInline` that isn't an image, or `AutolinkInline` with `mailto:` added for emails) whose source span contains `index`, or null.

- [ ] **Step 1: Write the failing tests**
  - `IsOpenable_WebAndMail_True`: `https://example.com`, `HTTP://x.org/a?b=1`, `mailto:a@b.com` give true.
  - `IsOpenable_Other_False`: `null`, `""`, `#`, `foo`, `/relative`, `file:///c:/x.exe`, `C:\x.exe`, `\\server\share`, `javascript:alert(1)`, `ftp://x` give false.
  - `UrlAt_InsideLinkText_ReturnsUrl`: `"see [docs](https://d.io) now"`, at the indexes of `[`, `d` (of docs), `h` (of https) and `)`, gives `https://d.io`.
  - `UrlAt_OutsideLink_ReturnsNull`: the same text at index 0 and at the index just after `)` gives null.
  - `UrlAt_Autolink_ReturnsUrl`: `"<https://a.b>"` at index 3 gives `https://a.b`, and `"<me@x.com>"` at index 3 gives `mailto:me@x.com`.
  - `UrlAt_Image_ReturnsNull`: `"![pic](https://a/p.png)"` at index 3 gives null.
  - `UrlAt_LinkInsideBold_ReturnsUrl`: `"**[b](https://b.c)**"` at index 3 gives `https://b.c`.
  - `UrlAt_ReferenceLink_ReturnsResolvedUrl`: `"[x][r]\n\n[r]: https://r.s"` at index 1 gives `https://r.s`.

- [ ] **Step 2: Run them and check they fail**
  Run: `dotnet test Scratchpad.Core.Tests --filter NoteLinksTests`. Expected: a compile error, because `NoteLinks` doesn't exist.

- [ ] **Step 3: Implement.**
  - `IsOpenable` uses `Uri.TryCreate(url, UriKind.Absolute, out var u)` and checks the scheme against the three allowed.
  - `UrlAt` uses `Markdown.Parse(markdown, NotePipeline.Create())` and `doc.Descendants<Inline>()` (or a walk), keeping links whose `Span.Start <= index <= Span.End`.

- [ ] **Step 4: Run** `dotnet test Scratchpad.Core.Tests`. Expected: all pass.

- [ ] **Step 5: Commit** `feat: core rules for which link is under the cursor and which links may open`

### Task 2: Preview links open on click

**Files:**
- Modify: `Scratchpad/MainWindow.xaml.cs`: constructor (near `PreviewViewer.Pipeline = …`, ~line 79), `PreviewScrollViewer_MouseDoubleClick` (~line 259), and the Hyperlink branch of the preview theming walk (~line 927).

**Interfaces:**
- Consumes: `NoteLinks.IsOpenable`.
- Produces: `private void OpenLink(string url)`, used by Task 3.

- [ ] **Step 1:** Add `OpenLink(string url)`:
  - It returns if the URL isn't `IsOpenable`.
  - It ignores the same URL again within 500 ms (fields `_lastOpenedUrl` and `_lastOpenedAt`).
  - Otherwise it calls `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })` inside a try/catch that swallows the exception.

- [ ] **Step 2:** In the constructor, add `CommandBindings.Add(new CommandBinding(Markdig.Wpf.Commands.Hyperlink, …))`:
  - `Executed` calls `OpenLink(e.Parameter as string)` and sets `e.Handled = true`.
  - `CanExecute` sets `e.CanExecute = NoteLinks.IsOpenable(e.Parameter as string)`.

- [ ] **Step 3:** In the theming walk's `Hyperlink` branch:
  - if `ToolTip == null`, set it to `NavigateUri?.OriginalString`;
  - set `ToolTipService.SetShowOnDisabled(inline, true)`.

  The cursor is already a hand on enabled Hyperlinks.

- [ ] **Step 4:** In `PreviewScrollViewer_MouseDoubleClick`, add `IsWithin<Hyperlink>(e.OriginalSource)` to the early-return condition, and update its comment.

- [ ] **Step 5:** Run `dotnet build Scratchpad.sln`. Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit** `feat: links in the preview open in the browser`

### Task 3: Ctrl+click in the editor

**Files:**
- Modify: `Scratchpad/MainWindow.xaml` (`PreviewMouseLeftButtonDown="EditorTextBox_PreviewMouseLeftButtonDown"` on `EditorTextBox`) and `Scratchpad/MainWindow.xaml.cs`.

**Interfaces:**
- Consumes: `NoteLinks.UrlAt`, `NoteLinks.IsOpenable` and `OpenLink` (Task 2).

- [ ] **Step 1:** Add the handler:
  - Only when `Keyboard.Modifiers == ModifierKeys.Control`, get `index = EditorTextBox.GetCharacterIndexFromPoint(e.GetPosition(EditorTextBox), snapToText: false)`.
  - If `index >= 0`, get `url = NoteLinks.UrlAt(EditorTextBox.Text, index)`.
  - If `NoteLinks.IsOpenable(url)`, call `OpenLink(url)` and set `e.Handled = true`. Otherwise leave the event alone.
  - Ctrl+Shift doesn't reach it (`Window_PreviewMouseLeftButtonDown` handles peek first, and the modifiers wouldn't equal Control anyway).

- [ ] **Step 2:** Run `dotnet build Scratchpad.sln`. Expected: 0 errors.

- [ ] **Step 3: Commit** `feat: Ctrl+click a link in the editor opens it`

**Manual checks on Windows:**
1. In the preview, hover a `[text](https://…)` link: you get a hand cursor and the address as a tooltip.
2. Click it: it opens in the browser once, and the note stays in the preview.
3. Double-click a link: it opens once, and the editor doesn't open.
4. `<https://…>` works.
5. `[x](foo)`: hovering shows `foo`, and clicking does nothing.
6. In the editor, Ctrl+click on a link's text or URL opens it. A plain click just moves the caret.
7. Ctrl+Shift+click still peeks.
