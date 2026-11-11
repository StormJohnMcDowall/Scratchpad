# Scratchpad

A single, always-at-hand markdown note for Windows. No "which file?" — just one
scratch file that lives in the tray, saves itself, and flips between raw markdown
and a rendered preview.

| Edit | Preview |
| --- | --- |
| ![Edit mode](docs/images/edit-mode.png) | ![Preview mode](docs/images/preview-mode.png) |

## Features

- **One note, always there** — tray icon, taskbar button, optional always-on-top, and a
  global hotkey (Win+Shift+N by default).
- **Autosave** — continuous atomic saves, plus local backups (hourly, on exit, and before
  any big delete).
- **Edit ↔ preview** (Ctrl+E) that keeps your place; double-click the preview to jump to
  that spot in the editor.
- **Rendered markdown** — clickable task checkboxes, links, strikethrough, and syntax
  highlighting for C#, JSON, JS, HTML/XML and CSS code blocks.
- **Editing helpers** — insert today's date (Ctrl+;), wrap a selection in `*` `_` `` ` `` `~`,
  Tab / Shift+Tab to indent or outdent blocks.
- **Peek** — slide the note out of the way for a few seconds (Ctrl+Shift+click).
- **Make it yours** — Post-it styling with light/dark themes (follows Windows), independent
  font families and sizes for editor and preview, configurable date format and hotkey.

The note is stored at `%LOCALAPPDATA%\Scratchpad\scratchpad.md`, with settings and
backups alongside it.

## Building

Requires the .NET 10 SDK and, to run the app, Windows with .NET Framework 4.8.

```powershell
dotnet build Scratchpad.sln -c Release
dotnet test Scratchpad.Core.Tests
```

The app is `Scratchpad\bin\Release\net48\Scratchpad.exe` — no installer, no admin rights
needed. Copy the folder wherever you like and run it once; it registers itself to start
at sign-in (per-user, `HKCU\...\Run`).

`Scratchpad.Core` (netstandard2.0) holds all the testable logic and builds on any platform;
the WPF shell in `Scratchpad` targets `net48` and only runs on Windows.
