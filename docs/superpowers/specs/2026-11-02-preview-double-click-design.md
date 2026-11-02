# Preview Double-Click and Keep-Your-Place — Design

## Intent

Reading a long note in the preview and then wanting to change something is
clumsy today. You have to click Preview (or press Ctrl+E) to get back to
the editor, and the editor opens **at the top of the note**, so you lose
your place and have to scroll back down to find what you were reading. The
user's words: *"The big failure is if you are scrolled far down in preview,
and you switch to edit and are back at the top and lose context."* Going the
other way has the same problem: the preview always opens at the top.

Two things fix this:

1. **Double-click the preview to edit there.** This is the quick way in.
2. **Keep your place whenever you switch** between editor and preview, in
   both directions, however you switch (the Preview button, Ctrl+E, or a
   double-click).

## Goals

- **Preview → editor (Preview button or Ctrl+E).** The paragraph at the
  top of the preview ends up at the top of the editor, and the cursor moves
  to the start of that paragraph. You can start typing where you were
  reading. (The user chose "move the cursor" over "leave it where it was".)
- **Editor → preview (Preview button or Ctrl+E).** The paragraph holding
  the top line of the editor ends up at the top of the preview.
- **Double-click in the preview** switches to the editor:
  - The scroll position is kept as above.
  - The cursor lands **roughly where you clicked**. Ideally that is the
    exact word. If the word can't be pinned down, it falls back to the
    start of the paragraph you clicked.
  - The user said: *"Ideally the exact place, but that is not mega
    important. Roughly would do."* Losing your place on screen is the real
    failure. A cursor that is a word or two off is not.
- **Double-clicking a task-list checkbox does not switch.** A checkbox
  click toggles it, as it does today. A second click lands on the checkbox
  too. Neither should throw you into the editor.
- "Paragraph" here means any block the preview shows on its own:
  - a paragraph, heading, or list item;
  - a code-block line, a table row, or a block-quote paragraph.

  Roughly right is fine. When a block is tall, landing at its start (not
  the exact line inside it) is acceptable.
- *Designer's call:* after switching to the editor by any route, the editor
  has keyboard focus, so typing works immediately without another click.

## Non-goals

- No live, side-by-side scroll syncing; there is still only one view at a
  time.
- No pixel-exact alignment. Matching to the paragraph is the target.
- Keeping the editor's text selection when switching to the preview and
  back is not a goal: the cursor moves, as chosen above.
- No new settings. This behaviour is always on.

## Anti-goals

- **Switching must never leave you at the top of a long note** when you
  weren't at the top before. This is the failure the feature exists to
  remove.
- Double-click must not interfere with checkbox clicks. The checkbox
  double-toggle bug was only just fixed and must stay fixed.
- A note that the map can't handle must never crash the app or break
  switching. That includes empty notes, odd or broken markdown, and text
  that ends without a final newline. The worst acceptable result is today's
  behaviour: you land at the top.

## Constraints

- Platform and dependencies:
  - WPF on .NET Framework 4.8. No new NuGet dependencies, no admin rights.
  - Markdig and Markdig.Wpf 0.5.0.1 as already used.
- The Shell can only be compiled in the dev sandbox, never run. That means:
  - The logic that can be tested belongs in `Scratchpad.Core`, with xUnit
    tests.
  - That logic is: markdown text → list of preview blocks with their source
    line/offset ranges; "which block holds this source line"; and "where
    in this block's source is this clicked word".
  - It must parse with the same pipeline the preview uses
    (`new MarkdownPipelineBuilder().UseTaskLists().Build()`, as in
    `TaskListToggler`) so its blocks line up with what the preview draws.
- How the preview is built (from investigating Markdig.Wpf's source):
  - The preview is a `MarkdownViewer` inside the outer `PreviewScrollViewer`,
    and `PreviewScrollViewer` is what scrolls.
  - The viewer rebuilds its `FlowDocument` only when the markdown text
    changes.
  - `RenderPreview()` runs on every switch to the preview, so the
    document, the WPF blocks and the source map are always built from the
    same text.
  - Scroll positions can only be measured after layout. Expect to defer the
    scroll with `Dispatcher.BeginInvoke` (or wait for layout) after showing
    a view.
- Known Markdig.Wpf quirks from earlier features:
  - A task-list checkbox is a `CheckBox` in an `InlineUIContainer` inside a
    `Paragraph`.
  - A `---` rule is a `Line` in an `InlineUIContainer` inside a `Paragraph`.
  - Both turn up as WPF blocks with no ordinary text, and the block mapping
    must not get out of step because of them.

## Left to the builder

- **Matching preview blocks to source blocks.** One way is to walk the
  `FlowDocument`'s leaf blocks in order alongside Markdig's leaf blocks, in
  the same order. Text matching or another approach is fine if it is more
  robust. Whichever is chosen, if the two sides disagree, fall back
  gracefully rather than mismatch badly.
- **Finding the clicked word.**
  - The likely route is the `Run` under the mouse (`e.OriginalSource`) or a
    `TextPointer` from the hit position, then searching for that text within
    the block's source.
  - If the word appears more than once in the block, pick the occurrence
    closest to the click, or the first one.
- **Measuring the top block.**
  - Preview: e.g. `ContentElement`/`TextPointer.GetCharacterRect` against
    `PreviewScrollViewer.VerticalOffset`.
  - Editor: `GetFirstVisibleLineIndex` / `GetLineIndexFromCharacterIndex` /
    `ScrollToLine`.
- **Where to hook double-click.** `MouseDoubleClick` on the scroll viewer,
  or `PreviewMouseLeftButtonDown` with `ClickCount == 2`. In either case,
  ignore it when the source is (inside) a `CheckBox`.
- **What double-clicking empty space does.** For example, below the last
  paragraph or in the margins: switch, and put the cursor at the nearest
  block, or at the end of the note for space below the last block.
- **Placing the clicked line.** After a double-click, if putting the top
  block at the top leaves the cursor off-screen, scroll it into view.
