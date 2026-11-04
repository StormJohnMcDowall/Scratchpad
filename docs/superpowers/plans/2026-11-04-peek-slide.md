# Peek Slide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Peek slides the note down off-screen and back, instead of hiding it. The taskbar button stays throughout.

**Architecture:**
- **Core:** a `PeekSlide` helper holds the easing maths, unit-tested.
- **Shell:** `MainWindow`'s Hide/Show peek is replaced by a small state machine (`Idle`, `SlidingOut`, `Away`, `SlidingBack`). It moves `Top` on each `CompositionTarget.Rendering` frame. Every way of ending or interrupting a peek goes through one `CancelPeek()`, which puts `Top` back at home.

**Tech Stack:** WPF on .NET Framework 4.8; netstandard2.0 Core with xUnit.

**Spec:** a revision agreed in chat on 2026-11-04 to the peek in `2026-11-04-peek-behind.md`. Reproduced:
- **The slide:**
  - The note slides **down** off-screen in about 200 ms, accelerating, instead of hiding. The taskbar button stays.
  - After the peek time it slides back up to exactly where it was, decelerating.
  - Z-order and always-on-top are unchanged.
- **Focus:** the note keeps focus while away; the user clicks the window underneath first.
- **Ending a peek early:**
  - A taskbar button click during a peek ends it and brings the note back. It must not leave the note minimised.
  - A tray click or tray → Show still ends it early.
- **Re-peeking:** a peek started mid-slide-back begins from the note's current spot.
- **Saving position:** exiting or hiding during a peek or mid-slide saves the real home position, never an off-screen one.
- **The animation switch:** one constant turns the animation off, so the note jumps instead.
- **Unchanged from the peek-behind plan:**
  - the triggers (Peek button, Ctrl+Shift+click);
  - the Settings seconds;
  - a tray left click ends a peek early, with focus.

## Global Constraints

- No new NuGet dependencies.
- `PeekSlideDuration` = 200 ms, a single `static readonly TimeSpan` in `MainWindow`. `TimeSpan.Zero` must mean "jump" with no frames.
- Only `Top` moves; `Left`, `Width` and `Height` are never touched by a peek.
- The off-screen target is `SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight`, so the note's top edge sits at the bottom of the whole desktop.
- No `Hide()` in the peek path any more. `ShowWithoutActivating` is removed.

## Review Focus

1. **Placement save during a peek.** Tray Exit, Alt+F4 (the note still has focus while away), hide to tray, or Windows closing the app must save the home `Top`, not an off-screen or mid-slide value.
   - `Window_Closing` calls `CancelPeek()` before reading `Top` (Task 2).
2. **Taskbar click during a peek.** If the note is active, Windows minimises it, which raises `StateChanged` Minimized. If it isn't active, Windows activates it, which raises `Activated`. Both must end the peek and leave the note Normal and on-screen.
   - Not every `Activated` should end it: the note is usually *already* active when a peek starts, and `ShowAndActivate` cancels first anyway (Task 2).
3. **Re-entrancy.**
   - A Ctrl+Shift+click while `SlidingBack` restarts the slide-out from the current `Top` and keeps the home position.
   - A second `StartPeek` while `SlidingOut` or `Away` is a no-op. That can happen through the keyboard, because the note keeps focus.
   - The Rendering handler must be detached whenever no slide is running (Task 2).
4. **Return when always-on-top is off.** The user clicked a window underneath, which rose above the note. On return, the note must come to the front without taking focus. Keep the `Topmost` true/false toggle at the end of the slide back (Task 2).
5. **Exit mid-slide.** `MarkExiting` must stop the timer and detach Rendering. Nothing may move the window after shutdown starts (Task 2).

---

### Task 1: PeekSlide easing

**Files:**
- Create: `Scratchpad.Core/PeekSlide.cs`
- Test: `Scratchpad.Core.Tests/PeekSlideTests.cs`

**Interfaces:**
- Produces: `public static class PeekSlide`, with `public static double Interpolate(double from, double to, double progress, bool accelerate)`.
  - `progress` is clamped to the range 0–1.
  - With `accelerate` true, the curve is ease-in (`p²`); with it false, ease-out (`1-(1-p)²`).

- [ ] **Step 1: Write the failing tests:**

```csharp
[Theory]
[InlineData(0.0, true, 100)]
[InlineData(1.0, true, 900)]
[InlineData(0.5, true, 300)]   // ease-in: 100 + 800 * 0.25
[InlineData(0.5, false, 700)]  // ease-out: 100 + 800 * 0.75
[InlineData(-1.0, true, 100)]  // clamped
[InlineData(2.0, false, 900)]  // clamped
public void Interpolate_FromHundredToNineHundred(double progress, bool accelerate, double expected)
    => Assert.Equal(expected, PeekSlide.Interpolate(100, 900, progress, accelerate), 6);

[Fact]
public void Interpolate_Upwards_EndsAtTarget()
    => Assert.Equal(100, PeekSlide.Interpolate(900, 100, 1.0, false), 6);
```

- [ ] **Step 2: Run the tests.** Run `dotnet test Scratchpad.Core.Tests --filter PeekSlideTests`. Expected: a compile failure, because `PeekSlide` doesn't exist yet.
- [ ] **Step 3: Implement** `Interpolate`.
- [ ] **Step 4: Run the full suite.** Run `dotnet test Scratchpad.Core.Tests`. Expected: everything passes (129 + 7).
- [ ] **Step 5: Commit** `feat: easing for the peek slide`.

### Task 2: Slide instead of hide

**Files:**
- Modify: `Scratchpad/MainWindow.xaml` (Window: `Activated="Window_Activated"`), `Scratchpad/MainWindow.xaml.cs` (peek region ~950–1010, `Window_StateChanged`, `Window_Closing`, `ShowAndActivate`, `MarkExiting`)

**Interfaces:**
- Consumes: `PeekSlide.Interpolate` (Task 1).
- Produces: `public void EndPeekEarly()`, unchanged in signature, so the existing App/tray wiring stays.

- [ ] **Step 1: Add the state.**
  - `private enum PeekState { Idle, SlidingOut, Away, SlidingBack }`.
  - Fields: `_peekState`, `double _peekHomeTop`, `double _slideFrom`, `double _slideTo`, `Stopwatch _slideClock`.
  - `private static readonly TimeSpan PeekSlideDuration = TimeSpan.FromMilliseconds(200);`, with a comment that `TimeSpan.Zero` turns the animation off.
- [ ] **Step 2: Add the slide engine.**
  - `private void BeginSlide(double to, PeekState state)`: record `from = Top`, start the clock, and attach `CompositionTarget.Rendering += OnSlideFrame` if it isn't already attached. With a zero duration, finish at once instead.
  - `OnSlideFrame`: set `Top = PeekSlide.Interpolate(from, to, elapsed / duration, accelerate: state == SlidingOut)`. When progress reaches 1, detach Rendering and call `FinishSlide()`.
  - `FinishSlide()`:
    - from `SlidingOut`: go to `Away` and start `_peekTimer`;
    - from `SlidingBack`: set `Top = _peekHomeTop`, go to `Idle`, then apply the `if (!Topmost) { Topmost = true; Topmost = false; }` raise (Review Focus 4).
- [ ] **Step 3: Rewrite the peek entry points.**
  - `StartPeek()`:
    - In `Idle`, set `_peekHomeTop = Top`.
    - In `SlidingBack`, keep the home value.
    - In `SlidingOut` or `Away`, return.
    - Then call `BeginSlide(VirtualScreenTop + VirtualScreenHeight, SlidingOut)`.
  - Timer tick: stop the timer; if not `_isExiting` and the state is `Away`, call `BeginSlide(_peekHomeTop, SlidingBack)`.
  - `private void CancelPeek()`: if not `Idle`, stop the timer, detach Rendering, set `Top = _peekHomeTop`, and set `Idle`.
  - `EndPeekEarly()`: if not `Idle`, call `ShowAndActivate()`.
  - Delete `ShowWithoutActivating`.
- [ ] **Step 4: Hook the interrupts.**
  - `ShowAndActivate()` calls `CancelPeek()` where it used to call `_peekTimer.Stop()`.
  - `MarkExiting()` calls `CancelPeek()`.
  - `Window_Closing` calls `CancelPeek()` first, before the placement save (Review Focus 1).
  - `Window_StateChanged`: if the window is Minimized and the state isn't `Idle`, set `WindowState = Normal`, then call `CancelPeek()`.
  - New `Window_Activated`: if the state is `SlidingOut` or `Away`, call `CancelPeek()`. A user can only activate an off-screen note on purpose: by taskbar, Alt+Tab or the tray. Comment that the note is normally already active when a peek starts, so this doesn't fire at the start.
- [ ] **Step 5: Build and test.** Run `dotnet build Scratchpad/Scratchpad.csproj` and `dotnet test Scratchpad.Core.Tests`. Expected: `0 Error(s)`, and all tests pass.
- [ ] **Step 6: Commit** `feat: peek slides the note off-screen instead of hiding it`.

## Manual check on Windows (for the user, after merge)

1. Click Peek. The note slides down out of sight, the taskbar button stays, and after ~3 s it slides back to exactly its spot. Check this with always-on-top both on and off. With it off, click a window underneath during the peek: the note still comes back in front, and that window keeps focus.
2. Is the slide smooth? If it stutters, tell me and I'll switch it to a jump.
3. During a peek, click the note's taskbar button. The note comes straight back, not minimised. Try it once without clicking anything else first, and once after clicking a window underneath.
4. As the note is sliding back, Ctrl+Shift+click it. It slides away again, and later returns to the original spot, not the mid-slide one.
5. During a peek, choose tray → Exit, then restart the app. The note opens in its normal place.
6. During a peek, press Alt+F4 (the note still has focus). It goes to the tray, and tray → Show puts it back in its normal place.
