# Scratchpad Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray/taskbar app that edits one fixed markdown scratch file, toggles between raw-source and rendered views, autosaves continuously, and can run always-on-top — installable and runnable with no admin rights.

**Architecture:** Logic that doesn't need Windows (file I/O, debouncing, window-bounds math, the startup-registry and single-instance abstractions) lives in `Scratchpad.Core`, a `netstandard2.0` class library with no WPF or Windows-only dependency in its public surface. It is unit-tested by `Scratchpad.Core.Tests` (targets `net10.0` — the SDK available in this build environment), which can build and run anywhere, including this Linux sandbox. The WPF shell (`Scratchpad` project, targets `net48`) is Windows-only, references `Scratchpad.Core`, and can only be built and run on the target Windows machine — its tasks (6-7) end in a manual verification checklist run on that machine instead of an automated test, and are the only parts this sandbox cannot execute itself.

**Tech Stack:** C#, .NET Framework 4.8 (WPF + Windows Forms for the tray icon) for the shipped app — chosen because it ships with Windows 10/11 already, so nothing new needs installing; `netstandard2.0` for the shared logic library; xUnit for tests; Markdig.Wpf for markdown rendering (renders straight to a WPF `FlowDocument`, no WebView2/browser-control runtime dependency).

**Spec:** [docs/superpowers/specs/2026-10-29-scratchpad-design.md](../specs/2026-10-29-scratchpad-design.md)

## Global Constraints

- No admin rights, and no new runtime/tool installs on the target machine — every mechanism used (HKCU registry, Startup-folder-free autostart, .NET Framework 4.8) must already work for a non-admin user.
- Exactly one markdown document, at a fixed, non-configurable path under `%LOCALAPPDATA%\Scratchpad\scratchpad.md` — never under Documents (redirected to OneDrive for Business on this machine).
- Autosave is continuous and atomic (write to a temp file, then rename over the real file) — never requires an explicit save action.
- Only one instance of the app may run at a time; a second launch activates the first instance's window instead of starting a second process.
- Closing the window (the X button) hides it to the tray; the app only actually exits via the tray menu's "Exit".
- The app launches at Windows login and starts minimized to the tray.
- No network or cloud dependency of any kind.

## Review Focus

- A first run with no existing scratch file must open to an empty, writable document, not crash or show an error (Task 1: DocumentStore).
- A window position restored from a previous session that is now fully off every connected screen (e.g. a docking station or second monitor was removed) must be clamped back onto the visible screen, not left unreachable (Task 3: WindowPlacement).
- Launching the app while it's already running must activate the existing window rather than start a second process that could race the first one's autosave writes (Task 5: SingleInstanceGuard).
- Rapid, continuous typing must be coalesced into a single debounced write, not one disk write per keystroke (Task 2: DebouncedAutosaveCoordinator).
- Re-enabling the startup entry after the executable has moved or been rebuilt at a different path must overwrite the stored path, not leave it pointing at a stale location (Task 4: StartupRegistration).

---

## Task 1: Solution Scaffolding & Document Storage

**Files:**
- Create: `Scratchpad.sln`
- Create: `Scratchpad.Core/Scratchpad.Core.csproj`
- Create: `Scratchpad.Core/DocumentStore.cs`
- Create: `Scratchpad.Core.Tests/Scratchpad.Core.Tests.csproj`
- Test: `Scratchpad.Core.Tests/DocumentStoreTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.DocumentStore` — `static string DefaultFilePath { get; }` (= `%LOCALAPPDATA%\Scratchpad\scratchpad.md`), `DocumentStore(string filePath)`, `string FilePath { get; }`, `string Load()`, `void Save(string content)`. Tasks 2 and 6 construct this with `DocumentStore.DefaultFilePath` and call `Load()`/`Save(string)`.

- [ ] **Step 1: Scaffold the solution and projects**

```bash
dotnet new sln -n Scratchpad
dotnet new classlib -n Scratchpad.Core -o Scratchpad.Core -f netstandard2.0
dotnet new xunit -n Scratchpad.Core.Tests -o Scratchpad.Core.Tests -f net10.0
rm Scratchpad.Core/Class1.cs Scratchpad.Core.Tests/UnitTest1.cs
dotnet sln add Scratchpad.Core/Scratchpad.Core.csproj Scratchpad.Core.Tests/Scratchpad.Core.Tests.csproj
dotnet add Scratchpad.Core.Tests/Scratchpad.Core.Tests.csproj reference Scratchpad.Core/Scratchpad.Core.csproj
```

Expected: `dotnet build` succeeds with no source files yet (an empty library/test project).

- [ ] **Step 2: Write the failing tests**

```csharp
public class DocumentStoreTests
{
    [Fact]
    public void DefaultFilePath_IsUnderLocalAppDataScratchpadFolder()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "scratchpad.md");
        Assert.Equal(expected, DocumentStore.DefaultFilePath);
    }

    [Fact]
    public void Load_CreatesEmptyFileWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        var content = store.Load();

        Assert.Equal("", content);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_WritesContentAtomicallyWithNoLeftoverTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        store.Save("hello");

        Assert.Equal("hello", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        store.Save("first");
        store.Save("second");

        Assert.Equal("second", store.Load());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter DocumentStoreTests`
Expected: FAIL to compile — `DocumentStore` does not exist.

- [ ] **Step 4: Implement `DocumentStore` in `Scratchpad.Core/DocumentStore.cs`**

`Load()`: if `FilePath` doesn't exist, create its parent directory and call `Save("")`; return the file's text either way. `Save(content)`: ensure the parent directory exists, write `content` to `FilePath + ".tmp"`, then atomically publish it — `File.Replace(tmp, FilePath, null)` if `FilePath` already exists, otherwise `File.Move(tmp, FilePath)`. Reusing the fixed `.tmp` name (not a random one) means there's never a leftover stray temp file to clean up.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter DocumentStoreTests`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add Scratchpad.sln Scratchpad.Core Scratchpad.Core.Tests
git commit -m "feat: add DocumentStore with atomic save"
```

---

## Task 2: Debounced Autosave Coordinator

**Files:**
- Create: `Scratchpad.Core/DebouncedAutosaveCoordinator.cs`
- Test: `Scratchpad.Core.Tests/DebouncedAutosaveCoordinatorTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.DebouncedAutosaveCoordinator` (implements `IDisposable`) — `DebouncedAutosaveCoordinator(Action<string> save, TimeSpan delay)`, `void NotifyChanged(string currentContent)`, `void Dispose()`. Task 6 constructs this with `save = documentStore.Save` and `delay = TimeSpan.FromMilliseconds(500)`, calling `NotifyChanged(EditorTextBox.Text)` on every `TextChanged`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class DebouncedAutosaveCoordinatorTests
{
    [Fact]
    public void CoalescesRapidChangesIntoASingleSave()
    {
        var saves = new List<string>();
        var signal = new ManualResetEventSlim();
        using var coordinator = new DebouncedAutosaveCoordinator(
            s => { saves.Add(s); signal.Set(); }, TimeSpan.FromMilliseconds(50));

        coordinator.NotifyChanged("a");
        coordinator.NotifyChanged("ab");
        coordinator.NotifyChanged("abc");

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Thread.Sleep(100); // ensure no further saves arrive
        Assert.Equal(new[] { "abc" }, saves);
    }

    [Fact]
    public void SavesAgainAfterDelayElapsesBetweenChanges()
    {
        var saves = new List<string>();
        var signal = new ManualResetEventSlim();
        using var coordinator = new DebouncedAutosaveCoordinator(
            s => { saves.Add(s); signal.Set(); }, TimeSpan.FromMilliseconds(50));

        coordinator.NotifyChanged("a");
        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        signal.Reset();
        coordinator.NotifyChanged("b");
        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));

        Assert.Equal(new[] { "a", "b" }, saves);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter DebouncedAutosaveCoordinatorTests`
Expected: FAIL to compile — `DebouncedAutosaveCoordinator` does not exist.

- [ ] **Step 3: Implement `DebouncedAutosaveCoordinator` in `Scratchpad.Core/DebouncedAutosaveCoordinator.cs`**

Hold the latest content behind a lock and a single `System.Threading.Timer`. `NotifyChanged` stores the latest content and resets the timer to fire once after `delay`; the timer callback invokes `save(latestContent)`. `Dispose` disposes the timer.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter DebouncedAutosaveCoordinatorTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/DebouncedAutosaveCoordinator.cs Scratchpad.Core.Tests/DebouncedAutosaveCoordinatorTests.cs
git commit -m "feat: add debounced autosave coordinator"
```

---

## Task 3: Window Placement — Clamping & Persistence

**Files:**
- Create: `Scratchpad.Core/WindowPlacement.cs`
- Create: `Scratchpad.Core/WindowPlacementStore.cs`
- Test: `Scratchpad.Core.Tests/WindowPlacementTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.WindowBounds` (struct: `double Left, Top, Width, Height`); `Scratchpad.Core.WindowPlacement.Clamp(WindowBounds requested, WindowBounds screenBounds) -> WindowBounds`; `Scratchpad.Core.WindowPlacementStore` — `static string DefaultFilePath { get; }` (= `%LOCALAPPDATA%\Scratchpad\window-placement.json`), `WindowPlacementStore(string filePath)`, `WindowBounds Load()` (returns `new WindowBounds { Left = 100, Top = 100, Width = 420, Height = 520 }` if the file is missing or unparseable), `void Save(WindowBounds bounds)`. Task 6 calls `Load()` on startup, passes the result and the real screen bounds (converted from `SystemParameters.VirtualScreen*`) to `Clamp`, and calls `Save(bounds)` when the window closes/hides. Task 6's `MainWindow` default `Width`/`Height` in XAML must match this same default (420x520).

- [ ] **Step 1: Write the failing tests**

```csharp
public class WindowPlacementTests
{
    static readonly WindowBounds Screen = new WindowBounds { Left = 0, Top = 0, Width = 1920, Height = 1080 };

    [Fact]
    public void Clamp_LeavesOnScreenRectUnchanged()
    {
        var rect = new WindowBounds { Left = 100, Top = 100, Width = 420, Height = 520 };
        Assert.Equal(rect, WindowPlacement.Clamp(rect, Screen));
    }

    [Fact]
    public void Clamp_MovesFullyOffscreenRectBackOntoScreen()
    {
        var rect = new WindowBounds { Left = -5000, Top = -5000, Width = 420, Height = 520 };

        var result = WindowPlacement.Clamp(rect, Screen);

        Assert.InRange(result.Left, Screen.Left, Screen.Left + Screen.Width - result.Width);
        Assert.InRange(result.Top, Screen.Top, Screen.Top + Screen.Height - result.Height);
    }

    [Fact]
    public void Clamp_AlignsToTopLeftWhenLargerThanScreen()
    {
        var rect = new WindowBounds { Left = 100, Top = 100, Width = 3000, Height = 2000 };

        var result = WindowPlacement.Clamp(rect, Screen);

        Assert.Equal(Screen.Left, result.Left);
        Assert.Equal(Screen.Top, result.Top);
    }
}

public class WindowPlacementStoreTests
{
    static readonly WindowBounds Default = new WindowBounds { Left = 100, Top = 100, Width = 420, Height = 520 };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "window-placement.json");
        Assert.Equal(Default, new WindowPlacementStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new WindowPlacementStore(path).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "window-placement.json");
        var store = new WindowPlacementStore(path);
        var bounds = new WindowBounds { Left = 42, Top = 7, Width = 600, Height = 300 };

        store.Save(bounds);

        Assert.Equal(bounds, store.Load());
    }
}
```

(`WindowBounds` needs value equality — implement it as a C# `struct` with all four fields, which gives field-wise equality for free via `Equals`/`==` overrides, or use a `readonly record struct` if the C# language version supports it.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter "WindowPlacementTests|WindowPlacementStoreTests"`
Expected: FAIL to compile — types don't exist.

- [ ] **Step 3: Implement `WindowBounds`, `WindowPlacement.Clamp`, and `WindowPlacementStore`**

`Clamp`: if `requested` already fits within `screenBounds` on both axes, return it unchanged; otherwise clamp `Left` into `[screenBounds.Left, screenBounds.Left + screenBounds.Width - requested.Width]` using `Math.Max`/`Math.Min` (and the same for `Top`/`Height`), so a window larger than the screen lands at the screen's top-left. `WindowPlacementStore`: serialize/deserialize `WindowBounds` as JSON via `System.Text.Json` (add the `System.Text.Json` NuGet package since `netstandard2.0` doesn't bundle it); `Load()` catches any file-read or JSON exception and returns the default; `Save` writes through the same temp-file-then-`File.Replace`/`Move` pattern as `DocumentStore`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter "WindowPlacementTests|WindowPlacementStoreTests"`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/WindowPlacement.cs Scratchpad.Core/WindowPlacementStore.cs Scratchpad.Core.Tests/WindowPlacementTests.cs
git commit -m "feat: add window placement clamping and persistence"
```

---

## Task 4: Startup Registration (Run at Login)

**Files:**
- Create: `Scratchpad.Core/StartupRegistration.cs`
- Test: `Scratchpad.Core.Tests/StartupRegistrationTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.IRunKeyRegistry` (`void SetValue(string name, string value)`, `string GetValue(string name)` returns `null` if absent, `void DeleteValue(string name)`); `Scratchpad.Core.RegistryRunKey : IRunKeyRegistry` (wraps `Microsoft.Win32.Registry.CurrentUser\Software\Microsoft\Windows\CurrentVersion\Run`); `Scratchpad.Core.StartupRegistration` — `public const string ValueName = "Scratchpad"`, `StartupRegistration(IRunKeyRegistry registry, string executablePath)`, `void Enable()`, `void Disable()`, `bool IsEnabledForCurrentPath()`. Task 7 constructs `new StartupRegistration(new RegistryRunKey(), Process.GetCurrentProcess().MainModule.FileName)` and calls `Enable()` on every app startup.

- [ ] **Step 1: Write the failing tests**

```csharp
class FakeRunKeyRegistry : IRunKeyRegistry
{
    public Dictionary<string, string> Values = new Dictionary<string, string>();
    public void SetValue(string name, string value) => Values[name] = value;
    public string GetValue(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public void DeleteValue(string name) => Values.Remove(name);
}

public class StartupRegistrationTests
{
    [Fact]
    public void Enable_SetsValueToQuotedExecutablePath()
    {
        var registry = new FakeRunKeyRegistry();
        new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe").Enable();

        Assert.Equal("\"C:\\Apps\\Scratchpad.exe\"", registry.Values[StartupRegistration.ValueName]);
    }

    [Fact]
    public void IsEnabledForCurrentPath_FalseWhenStoredPathDiffers()
    {
        var registry = new FakeRunKeyRegistry();
        registry.Values[StartupRegistration.ValueName] = "\"C:\\Old\\Scratchpad.exe\"";

        var result = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe").IsEnabledForCurrentPath();

        Assert.False(result);
    }

    [Fact]
    public void IsEnabledForCurrentPath_TrueAfterEnable()
    {
        var registry = new FakeRunKeyRegistry();
        var registration = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe");

        registration.Enable();

        Assert.True(registration.IsEnabledForCurrentPath());
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var registry = new FakeRunKeyRegistry();
        var registration = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe");
        registration.Enable();

        registration.Disable();

        Assert.Null(registry.GetValue(StartupRegistration.ValueName));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter StartupRegistrationTests`
Expected: FAIL to compile — types don't exist.

- [ ] **Step 3: Implement `IRunKeyRegistry`, `RegistryRunKey`, and `StartupRegistration`**

`Enable()` calls `registry.SetValue(ValueName, "\"" + executablePath + "\"")`. `IsEnabledForCurrentPath()` compares `registry.GetValue(ValueName)` to that same quoted string. `Disable()` calls `registry.DeleteValue(ValueName)`. `RegistryRunKey` opens `Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)` (creating it if needed via `CreateSubKey`) and forwards each method to `SetValue`/`GetValue`/`DeleteValue` on that key — add the `Microsoft.Win32.Registry` NuGet package to `Scratchpad.Core` so this compiles under `netstandard2.0`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter StartupRegistrationTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/StartupRegistration.cs Scratchpad.Core.Tests/StartupRegistrationTests.cs
git commit -m "feat: add startup registration via HKCU Run key"
```

---

## Task 5: Single-Instance Guard

**Files:**
- Create: `Scratchpad.Core/InstanceMessage.cs`
- Create: `Scratchpad.Core/SingleInstanceGuard.cs`
- Test: `Scratchpad.Core.Tests/SingleInstanceGuardTests.cs`

**Interfaces:**
- Produces: `Scratchpad.Core.InstanceMessage.Show` (`const string` = `"SHOW"`), `Scratchpad.Core.InstanceMessage.IsShowCommand(string message) -> bool`; `Scratchpad.Core.SingleInstanceGuard` (implements `IDisposable`) — `public const string DefaultMutexName = "Scratchpad-SingleInstance-Mutex"`, `public const string DefaultPipeName = "Scratchpad-SingleInstance-Pipe"`, `SingleInstanceGuard(string mutexName, string pipeName)`, `bool TryAcquire()`, `void ListenForShowRequests(Action onShowRequested)`, `void NotifyExistingInstanceToShow()`, `void Dispose()`. Task 7 constructs this with `SingleInstanceGuard.DefaultMutexName`/`DefaultPipeName` and wires `TryAcquire`/`ListenForShowRequests`/`NotifyExistingInstanceToShow` into app startup.

- [ ] **Step 1: Write the failing tests**

```csharp
public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuard_FailsToAcquire_WhenFirstHoldsMutex()
    {
        var mutexName = "Test-" + Guid.NewGuid();
        var pipeName = "Test-" + Guid.NewGuid();
        using var first = new SingleInstanceGuard(mutexName, pipeName);
        using var second = new SingleInstanceGuard(mutexName, pipeName);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void NotifyExistingInstance_InvokesCallbackOnFirstGuard()
    {
        var mutexName = "Test-" + Guid.NewGuid();
        var pipeName = "Test-" + Guid.NewGuid();
        using var first = new SingleInstanceGuard(mutexName, pipeName);
        using var second = new SingleInstanceGuard(mutexName, pipeName);
        var signal = new ManualResetEventSlim();
        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
        first.ListenForShowRequests(() => signal.Set());

        second.NotifyExistingInstanceToShow();

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Scratchpad.Core.Tests --filter SingleInstanceGuardTests`
Expected: FAIL to compile — types don't exist.

- [ ] **Step 3: Implement `InstanceMessage` and `SingleInstanceGuard`**

`TryAcquire()` wraps `new Mutex(initiallyOwned: true, mutexName, out bool createdNew)`, returning `createdNew`. `ListenForShowRequests(onShowRequested)` starts a background loop that opens a `NamedPipeServerStream(pipeName)`, waits for a connection, reads a line, and if `InstanceMessage.IsShowCommand(line)` invokes `onShowRequested`, then loops to wait for the next connection. `NotifyExistingInstanceToShow()` connects a `NamedPipeClientStream(".", pipeName)` with a short timeout, writes `InstanceMessage.Show` followed by a newline, and flushes. `Dispose()` releases the mutex and stops the listener loop.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Scratchpad.Core.Tests --filter SingleInstanceGuardTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add Scratchpad.Core/InstanceMessage.cs Scratchpad.Core/SingleInstanceGuard.cs Scratchpad.Core.Tests/SingleInstanceGuardTests.cs
git commit -m "feat: add single-instance guard via named mutex and pipe"
```

---

## Task 6: Main Window — Editor, Preview & Always-on-Top

*This task wires already-tested `Scratchpad.Core` units into the WPF shell. The shell only builds on Windows (WPF is a Windows-only workload), so this sandbox cannot run it — each step's result is verified manually on the target Windows machine instead of via `dotnet test`.*

**Files:**
- Create: `Scratchpad/Scratchpad.csproj` (WPF app, `net48`, references `Scratchpad.Core`)
- Create: `Scratchpad/App.xaml` / `Scratchpad/App.xaml.cs` (minimal — startup wiring completed in Task 7)
- Create: `Scratchpad/MainWindow.xaml` / `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `DocumentStore.DefaultFilePath`, `DocumentStore.Load()/Save(string)` (Task 1); `DebouncedAutosaveCoordinator` (Task 2); `WindowBounds`, `WindowPlacement.Clamp`, `WindowPlacementStore` (Task 3).
- Produces: `Scratchpad.MainWindow.ShowAndActivate()` (shows the window, restores it if minimized, and brings it to the foreground) and `Scratchpad.MainWindow.MarkExiting()` (sets an internal flag so the next `Closing` event is allowed to actually close instead of hiding). Task 7 calls both.

- [ ] **Step 1: Scaffold the WPF project**

```bash
dotnet new wpf -n Scratchpad -o Scratchpad
dotnet sln add Scratchpad/Scratchpad.csproj
dotnet add Scratchpad/Scratchpad.csproj reference Scratchpad.Core/Scratchpad.Core.csproj
dotnet add Scratchpad/Scratchpad.csproj package Markdig.Wpf
```

Edit `Scratchpad.csproj`'s `<TargetFramework>` to `net48` (the template defaults to a newer TFM).

- [ ] **Step 2: Build `MainWindow.xaml`**

A `DockPanel`: a top toolbar (`StackPanel`, horizontal) with two `ToggleButton`s — `x:Name="PreviewToggleButton"` (content "Preview") and `x:Name="AlwaysOnTopToggleButton"` (content "Always on Top") — and below it a `Grid` containing both `x:Name="EditorTextBox"` (a `TextBox` with `AcceptsReturn="True"`, `AcceptsTab="True"`, `TextWrapping="Wrap"`, `VerticalScrollBarVisibility="Auto"`, `FontFamily="Consolas"`) and `x:Name="PreviewViewer"` (a `markdig:MarkdownViewer`, `xmlns:markdig="clr-namespace:Markdig.Wpf;assembly=Markdig.Wpf"`, wrapped in a `ScrollViewer`) occupying the same cell — only one visible at a time. Window `Width="420" Height="520"` (matching `WindowPlacementStore`'s default). If `MarkdownViewer` renders unstyled at runtime, merge Markdig.Wpf's packaged style resource dictionary into `App.xaml`'s `Application.Resources` (consult the installed package's contents for the exact resource path).

- [ ] **Step 3: Implement `MainWindow.xaml.cs`**

Fields: a `DocumentStore` built from `DocumentStore.DefaultFilePath`, a `WindowPlacementStore` built from `WindowPlacementStore.DefaultFilePath`, a `DebouncedAutosaveCoordinator` (`delay: TimeSpan.FromMilliseconds(500)`), and a `bool _isExiting`.

- `Loaded`: set `EditorTextBox.Text = documentStore.Load()`; load bounds via `placementStore.Load()`, clamp with `WindowPlacement.Clamp` against a `WindowBounds` built from `SystemParameters.VirtualScreenLeft/Top/VirtualScreenWidth/VirtualScreenHeight`, and apply the result to `Left`/`Top`/`Width`/`Height`.
- `EditorTextBox.TextChanged`: call `autosave.NotifyChanged(EditorTextBox.Text)`.
- `PreviewToggleButton.Click`: when checked, set `PreviewViewer.Markdown = EditorTextBox.Text` and swap visibility (editor `Collapsed`, preview `Visible`); when unchecked, the reverse. Bind Ctrl+E to toggle `PreviewToggleButton.IsChecked` (via a `KeyBinding`/`PreviewKeyDown` handler).
- `AlwaysOnTopToggleButton.Click`: set `this.Topmost = AlwaysOnTopToggleButton.IsChecked == true`. Bind Ctrl+T the same way.
- `Closing`: always `placementStore.Save(new WindowBounds { Left = Left, Top = Top, Width = Width, Height = Height })` first; if `!_isExiting`, set `e.Cancel = true` and call `Hide()` instead of closing.
- `ShowAndActivate()`: `Show(); WindowState = WindowState.Normal; Activate();`
- `MarkExiting()`: `_isExiting = true;`

- [ ] **Step 4: Manually verify**

On the target Windows machine: `dotnet build Scratchpad -c Release`, run the exe, type some markdown, press Ctrl+E — confirm it renders (headings/lists/code block); press Ctrl+E again — confirm it returns to the raw source unchanged; press Ctrl+T — confirm the window stays on top of other apps; resize/move the window, close and reopen the app — confirm position and size are restored.

- [ ] **Step 5: Commit**

```bash
git add Scratchpad Scratchpad.sln
git commit -m "feat: add main window with editor/preview toggle and always-on-top"
```

---

## Task 7: Tray Icon & Application Entry Point

*Like Task 6, this is Windows-only wiring, verified manually on the target machine.*

**Files:**
- Create: `Scratchpad/TrayIconManager.cs`
- Modify: `Scratchpad/Scratchpad.csproj` (add `<UseWindowsForms>true</UseWindowsForms>` for `NotifyIcon`)
- Modify: `Scratchpad/App.xaml` / `Scratchpad/App.xaml.cs`

**Interfaces:**
- Consumes: `MainWindow.ShowAndActivate()`, `MainWindow.MarkExiting()` (Task 6); `SingleInstanceGuard`, `InstanceMessage` (Task 5); `StartupRegistration`, `RegistryRunKey` (Task 4).
- Produces: `Scratchpad.TrayIconManager` (implements `IDisposable`) — `TrayIconManager(Action onShowRequested, Action onExitRequested)`. Nothing later depends on this; it's the top of the dependency graph.

- [ ] **Step 1: Implement `TrayIconManager`**

Wraps a `System.Windows.Forms.NotifyIcon` (`Visible = true`, `Icon = System.Drawing.SystemIcons.Application` as a placeholder — swap in a custom `.ico` embedded resource later if desired) with a `ContextMenuStrip` holding two `ToolStripMenuItem`s: "Show" (invokes `onShowRequested`) and "Exit" (invokes `onExitRequested`). Double-click on the icon also invokes `onShowRequested`. `Dispose()` disposes the `NotifyIcon`.

- [ ] **Step 2: Wire `App.xaml.cs`'s startup**

Override `OnStartup`:
1. Create `var guard = new SingleInstanceGuard(SingleInstanceGuard.DefaultMutexName, SingleInstanceGuard.DefaultPipeName);`
2. If `!guard.TryAcquire()`: call `guard.NotifyExistingInstanceToShow()`, then `Shutdown()` and return — no window or tray icon is created for the second launch.
3. Otherwise: construct `var mainWindow = new MainWindow();` (not shown — it starts minimized to the tray); call `new StartupRegistration(new RegistryRunKey(), Process.GetCurrentProcess().MainModule.FileName).Enable();`; call `guard.ListenForShowRequests(() => Dispatcher.Invoke(mainWindow.ShowAndActivate));`; construct `var tray = new TrayIconManager(onShowRequested: mainWindow.ShowAndActivate, onExitRequested: () => { mainWindow.MarkExiting(); tray.Dispose(); guard.Dispose(); Shutdown(); });`.
4. Do not call `mainWindow.Show()` — the app starts with no visible window, tray icon only.

- [ ] **Step 3: Manually verify end-to-end, on the target Windows machine**

- `dotnet build Scratchpad -c Release`, run the exe: confirm no window appears but a tray icon does.
- Double-click the tray icon (or use "Show"): the window appears with the last-saved content loaded.
- Type text, wait a couple of seconds, kill the process in Task Manager, relaunch: confirm the typed text survived.
- Launch a second copy of the exe while the first is running: confirm only one process/window exists, and the first window activates.
- Click the window's X: it disappears but the process and tray icon remain; "Show" brings it back.
- Use the tray's "Exit": confirm the process actually terminates in Task Manager.
- Run `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v Scratchpad`: confirm it points at the built exe's path.
- Log off and back on (or restart): confirm the app auto-launches minimized to the tray.

- [ ] **Step 4: Commit**

```bash
git add Scratchpad
git commit -m "feat: add tray icon and wire single-instance, startup, and app entry point"
```
