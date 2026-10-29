# Scratchpad — Design

## Intent

A constant, frictionless place to scribble a note or stash a snippet on a
locked-down Windows work machine. The problem it solves: existing tools
either assume you might want any of many files (VS Code, Notepad — you
have to find or create "the" scratch file each time) or are always
reachable but plain text only (Sticky Notes — no markdown). Scratchpad is
a single, fixed markdown document that is always one click or keystroke
away, with just enough structure (headings, lists, code blocks) to be
more useful than a sticky note, without ever becoming a file manager.

## Goals

- One fixed markdown document — no "which file" friction, ever.
- Always reachable: tray icon, taskbar icon, and an optional
  "always on top" mode, usable in any combination.
- Toggle between editing the raw markdown source and viewing it
  rendered.
- Autosave continuously as the user types — no save step, no risk of
  losing a note.
- Runs as a normal user on a Windows machine with no admin rights and
  no ability to install new software.

## Non-goals

- Multiple documents, notebooks, tabs, or a file browser of any kind.
- Cloud sync, accounts, or any network dependency.
- Rich formatting beyond standard markdown (no WYSIWYG rich text editor).
- Mobile/other-OS support.

## Anti-goals

None identified — the user explicitly confirmed there's no direction
this could grow in that would make them stop wanting to use it. The
non-goals above exist to keep it that way: this stays a single-purpose
scratchpad, not a general notes app.

## Constraints

- **No admin rights** on the target machine, and no ability to install
  new runtimes or tools.
- IT policy is otherwise unknown in detail, but **de-risked**: the user
  already builds and runs a self-compiled WPF app from GitHub source on
  this machine today (a monitor-brightness tool that lives in the tray),
  proving that a standalone, self-built executable can run persistently
  outside the Visual Studio debugger. Scratchpad rides the same proven
  path.
- Available toolchain on the machine: .NET, Visual Studio, Node.js,
  VS Code.

## Decided (by the user)

- Single fixed markdown document, not a multi-file editor.
- Edit/rendered-view toggle (not a split-pane preview).
- Always-on-top is an optional toggle, not the only way to show the
  window — tray icon, taskbar icon, and always-on-top must all work,
  and in combination.
- Continuous autosave; never require an explicit save action.
- No scope concerns / no anti-goals beyond the non-goals above.

## Decided (my call, as the builder)

- **Platform: WPF on .NET, in C#.** This is the lowest-risk option
  given the constraints: it's proven to run standalone on this exact
  machine already (the brightness app), needs no new install (the
  runtime ships with Windows/Visual Studio), and gives native tray and
  always-on-top support directly. Electron (via the available Node
  toolchain) was considered but rejected as the default — it would mean
  shipping and running a new, larger, unsigned binary of a kind that
  hasn't already been proven on this machine, for no benefit the user
  asked for.
- **Launch at Windows login, starting minimized to the tray**, so the
  app is simply always there without the user thinking about it. This
  can be disabled if it turns out to be unwanted.
- The single document lives at a fixed, non-configurable path under
  `%LOCALAPPDATA%` (exact sub-path is an implementation detail for the
  plan). **Not** under Documents: on this machine Documents is redirected
  to OneDrive for Business, and writing on every keystroke there would
  risk sync-lock contention or "conflicted copy" duplicates.
  `%LOCALAPPDATA%` is local-only and not synced.
- Exact markdown rendering approach (rendering library, fidelity of
  support for tables/code blocks/checklists/links) and the precise
  mechanism for toggling edit/view (keyboard shortcut and/or button) are
  implementation details for the plan, not user-visible commitments made
  here.
- Window position and size are remembered across restarts.
- **Single instance only.** If the exe is launched while already running
  (e.g. autostart at login plus a stray double-click later), the second
  launch activates/shows the existing window instead of starting a
  second process. Two processes autosaving to the same fixed file could
  otherwise race and clobber each other's edits — the one failure mode
  the autosave goal explicitly exists to prevent.
- **Closing the window (the X button) hides it to the tray rather than
  exiting the app.** This follows directly from the "always reachable"
  goal and the auto-launch-at-login default — the app shouldn't become
  unreachable until next login just because the window was closed. The
  only way to actually quit is an "Exit" item on the tray icon's context
  menu.
- **Autosave writes are atomic**: write to a temp file, then rename it
  over the real document. This protects against a corrupted/partial file
  if the process is killed or the machine loses power mid-write, which
  is necessary for the stated "never lose a note" goal to actually hold.

## What's left to the builder (the plan)

- Concrete project structure and WPF window/control choices.
- Markdown rendering library and technique.
- Exact sub-path/filename under `%LOCALAPPDATA%` and the autosave
  debounce timing.
- Tray icon design/assets, context menu contents (e.g. Show, Always on
  Top, Exit).
- Startup registration mechanism (e.g. registry Run key vs. a Startup
  folder shortcut) — must not require admin rights.
- Keyboard shortcut(s) for toggling edit/rendered view and for
  always-on-top.
