# Backups — Design

## Intent

Scratchpad keeps one note and saves it continuously. Undo only lasts for
the session, and the autosave overwrites the file within half a second. So
one slip, such as selecting everything and typing or pasting over it,
replaces the note for good once the app closes or the slip is noticed too
late. Nothing has been lost yet; this is insurance. The user's worry is **a
slip in the app**, not losing the laptop.

The fix is plain dated copies of the note in a folder. When the user
notices a slip, perhaps hours or days later, they open the folder, find a
copy from before it and take the text back by hand. The user's words: *"I
manually futz with files."*

## Goals

- **Where the copies go.** `%LOCALAPPDATA%\Scratchpad\backups\`, next to
  the note. They're on this laptop only, which is enough because slips are
  the worry. The folder is created when first needed.
- **What each copy is.** An ordinary UTF-8 `.md` file containing the
  whole note, openable in VS Code or Notepad. The names sort by time in
  Explorer, e.g. `scratchpad 2026-11-04 153012.md` (local time, to the
  second, so two copies in the same minute never collide).
- **Regular copies.**
  - While the user is editing: at most one copy an hour, and only when the
    text differs from the newest existing copy. An idle note, or one that
    was merely opened, adds no copies.
  - On exit: when the app closes through the tray's Exit (or Windows shuts
    it down), one more copy is taken if the text differs from the newest
    copy.
  - *Designer's call:* "the newest copy" means the newest **regular**
    copy, both for the hourly clock and for the "differs" comparison.
    Before-big-delete copies are extras. They neither restart the clock
    nor count as the latest text, because they hold the old text.
- **Copies before a big delete** (the user's idea). Some single edits
  remove a large part of the note, for example:
  - select all then Delete or Backspace;
  - cutting a large selection;
  - pasting or typing over a large selection;
  - undoing a large paste.

  For any such edit, the text **from just before the edit** is copied
  straight away. It doesn't wait for the hourly copy, and the hourly limit
  doesn't apply.
  - **The threshold (agreed with the user).** The edit removes at least
    **half the note**, provided the note had at least **100 characters**
    before the edit; or the edit removes at least **1,000 characters**,
    whatever the note's size.
  - **How "removed" is measured.** Take the text before and after the
    edit and drop their common start and common end. What's left of the
    before-text is what was removed. Text added by the same edit doesn't
    cancel the removal, so pasting new text over a selection still counts
    everything that was selected.
  - **Naming.** These copies are named so they stand out:
    `scratchpad 2026-11-04 153012 before big delete.md`.
  - Cutting a big section to move it elsewhere also makes a copy. The user
    accepted that; it costs nothing.
  - **Indenting** a large selection (Tab or Shift+Tab via `LineIndenter`)
    changes the start of every line. By this measure it removes everything
    between the first and last changed line, so it can make a
    before-big-delete copy too. *Designer's call (the user didn't mind):*
    accept it, with no special case. The copy is harmless and could even
    help after a bad indent.
- **How long copies are kept.** Pruning happens whenever a copy is
  written. It applies to regular copies and before-big-delete copies alike:
  - Everything from the **last 48 hours** is kept.
  - Older copies dated on or after the local calendar day 30 days before
    today: for each local calendar day, only the newest copy is kept.
  - Anything dated before that day is deleted.
  - *Designer's call:* times come from the file names, in local time.
    "Today" is `now.Date`, and the cut-off day is `now.Date.AddDays(-30)`.
    Within a day, regular and before-big-delete copies compete equally:
    the newest copy of either kind wins.
  - Only files in the backups folder whose names match the app's own
    pattern are ever deleted. Anything else the user puts there is left
    alone.
- **Finding them.** Settings gets a **Backups** section with an **Open
  backups folder** button. It opens the folder in Explorer, creating it
  first if it doesn't exist yet. Next to the button is a line of text, e.g.
  "Copies of the note from the last 30 days."

## Non-goals

- No restore screen, no list or preview of copies, and no diff view inside
  the app.
- No settings for timing, thresholds or how long copies are kept.
- No copying to OneDrive or anywhere off the laptop.
- No backups of the settings files.
- "Reset to defaults" doesn't touch backups.

## Anti-goals

- **A backup failure must never lose or block the note.**
  - A full disk, a locked file or a missing folder must never crash the
    app.
  - It must never stop the autosave or delay typing noticeably.
  - Backups are best-effort: swallow and move on, like the settings
    stores. The autosave of the note itself is the core promise and stays
    exactly as it is.
- **Never delete anything that isn't one of the app's own copies.**
- **The before-big-delete copy must hold the old text, not the new.**
  Copying after the edit would defeat the feature.
- **An empty or near-empty note doesn't spam copies.** The 100-character
  floor on the half-the-note rule, and the "only if different" rule on
  regular copies, both exist for this.

## Constraints

- WPF on .NET Framework 4.8 (`Scratchpad`) and `Scratchpad.Core` on
  netstandard2.0. No new NuGet dependencies, no admin rights.
- The Shell can only be compiled in the dev sandbox, never run. Everything
  that can be tested belongs in `Scratchpad.Core` with xUnit tests:
  - the removed-characters measure and the big-delete rule;
  - file naming, and reading a copy's time back from its name;
  - the "is an hourly copy due?" decision;
  - the pruning rule. Take "now" as a parameter so the tests can fix the
    clock;
  - writing and pruning against a temp folder.
- How the note is saved today:
  - `DocumentStore` writes `scratchpad.md` atomically.
  - `DebouncedAutosaveCoordinator` calls it 500 ms after the last change,
    on a thread-pool timer thread, not the UI thread.
  - `MainWindow.EditorTextBox_TextChanged` notifies it on every change.
  - The window's `Closing` flushes it on exit.
  - The editor text is first loaded in `Window_Loaded`. Loading the text
    is not a user edit and must not trigger a before-big-delete copy.
- Clicking a task checkbox in the preview changes the text too. A toggle
  removes 1 character, so it never trips the threshold.

## Left to the builder

- **Where the hourly check runs.** For example, hook it into the save path
  that the autosave already calls, so it runs off the UI thread, or use a
  separate timer. The newest copy's time can come from its file name, or
  be remembered in memory after the first look.
- **Detecting the big delete.** Keep the previous text in `MainWindow` and
  compare on each `TextChanged`. `TextChangedEventArgs.Changes` is a
  possible shortcut, but the Core string measure is the tested definition.
  Writing one small file synchronously on the UI thread is acceptable; so
  is queueing it, as long as the old text is captured at once.
- **Text encoding:** UTF-8 to match the note. Whether to write a BOM is
  the builder's choice, and should match what `DocumentStore` writes.
- **Layout of the Backups section** in Settings. Put it after the other
  groups, before Reset, in the existing style.
- **Opening Explorer:** e.g. `Process.Start("explorer.exe", folder)`,
  wrapped in try/catch.

## Manual checks on Windows

1. Type into the note for a while. A copy appears in
   `%LOCALAPPDATA%\Scratchpad\backups\`, and no second one appears within
   the hour unless something below triggers it.
2. Select all and press Delete. A `… before big delete.md` copy appears at
   once, holding the full text from before the delete. Undo still works
   as usual.
3. Select all and paste a short snippet over a long note. A
   before-big-delete copy holds the old note.
4. Delete a sentence or two, or tick a checkbox in the preview. No new
   copy appears.
5. Exit from the tray after editing. A final copy is written. Exiting
   again without editing writes nothing.
6. Settings → **Open backups folder** opens Explorer on the folder, even
   on a fresh install where the folder doesn't exist yet.
7. Drop an unrelated file into the backups folder. It's never deleted.
