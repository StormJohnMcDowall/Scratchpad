# Post-it Restyle & Custom Window Chrome Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle `MainWindow` to a pale-yellow Post-it look (toolbar and buttons included, not just the writing surface) and replace its native Windows title bar with a single custom hide-to-tray control, while keeping the window draggable and resizable.

**Architecture:** This is entirely `MainWindow.xaml`/`MainWindow.xaml.cs` work — no `Scratchpad.Core` involvement, since there's no persisted state or platform-agnostic logic here, only WPF presentation. Task 1 restyles colors/buttons without touching window chrome or layout structure, so it's reviewable independently of the riskier chrome work. Task 2 replaces the native title bar with `WindowStyle="None"` + `System.Windows.Shell.WindowChrome`, rebuilding drag-to-move (`DragMove()` on empty toolbar space) and edge-resize (`WindowChrome.ResizeBorderThickness`) deliberately rather than relying on `WindowChrome.CaptionHeight`, which would silently reintroduce double-click-to-maximize and Aero-snap — both explicitly out of scope. Like every prior Shell-only task in this codebase, both tasks are verified by `dotnet build Scratchpad -c Release` (this sandbox can compile the WPF shell via `EnableWindowsTargeting` but never run it) plus a manual-verification checklist on the target Windows machine.

**Tech Stack:** WPF/C#, `net48` — no new dependencies; `System.Windows.Shell.WindowChrome` ships in `PresentationFramework`, already referenced.

**Spec:** [docs/superpowers/specs/2026-10-29-post-it-restyle-design.md](../specs/2026-10-29-post-it-restyle-design.md)

## Global Constraints

- The *entire* `MainWindow` — toolbar, buttons, writing surface — uses the approved pale-yellow palette, not just the text area.
- The native title bar is replaced entirely by exactly one custom control: hide-to-tray, reusing the existing `Window_Closing` path (i.e. it just calls `Close()`, same as today's native X button) — no separate exit control, no minimize, no maximize.
- The window stays draggable (clicking/holding empty toolbar space) and resizable (dragging an edge) — both free today via native chrome, both rebuilt deliberately once it's gone.
- `SettingsWindow` is untouched — out of scope for this plan.
- No new NuGet packages.

## Review Focus

- Dragging the window via empty toolbar space must work, but clicking an actual toolbar button must never also start a window drag — this depends on using the bubbling `MouseLeftButtonDown` event (which a button's own click-handling marks `Handled` before it reaches a parent), not `PreviewMouseLeftButtonDown` (Task 2: WPF-only, no automated test; pin the exact event and manually verify both directions).
- The new hide button must not steal keyboard focus from the editor on click, consistent with the `Focusable="False"` convention every other toolbar button in this codebase already follows (a real bug a past review on this project caught for the zoom buttons) (Task 2).
- The window must remain resizable by dragging an edge with no native border to grab — `WindowChrome.ResizeBorderThickness` must actually produce working resize hit-testing, a genuine WPF/.NET-Framework risk area this sandbox cannot verify by running the app (Task 2: manual verification required on the target machine).
- The rendered preview's background must visually match the new palette, not show Markdig.Wpf's own default background underneath — setting `Background` on the `ScrollViewer` wrapping `MarkdownViewer` may not be sufficient if the `FlowDocument` itself paints an opaque background on top (Task 1: manual verification required, since this sandbox cannot render WPF to check).
- Every button/`ToggleButton`'s checked/pressed visual feedback (e.g. the `Preview` and `Always on Top` toggles) must still be visible after the custom `ControlTemplate` replaces WPF's default chrome — a naive template override is a common way to silently lose that feedback entirely (Task 1: pin the `IsChecked`/`IsMouseOver`/`IsPressed` triggers explicitly).

---

## Task 1: Post-it Color Palette

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `Window.Resources` brush keys `PostItBackgroundBrush` (`#FFF3A3`), `PostItToolbarBrush` (`#FDEE8C`), `PostItBorderBrush` (`#E6D978`), `PostItButtonBrush` (`#FFE066`), `PostItButtonHoverBrush` (`#FFD633`), `PostItButtonPressedBrush` (`#F0C419`), `PostItTextBrush` (`#5C4F1A`); implicit `Style` resources for `Button` and `ToggleButton` built from these. Task 2 consumes `PostItToolbarBrush` (moves it from the toolbar `StackPanel` to the new wrapping `Grid`) and relies on the `Button` style applying automatically to the new hide button.

- [ ] **Step 1: Add the palette brushes and button styles to `Window.Resources`**

```xml
<Window.Resources>
    <SolidColorBrush x:Key="PostItBackgroundBrush" Color="#FFF3A3" />
    <SolidColorBrush x:Key="PostItToolbarBrush" Color="#FDEE8C" />
    <SolidColorBrush x:Key="PostItBorderBrush" Color="#E6D978" />
    <SolidColorBrush x:Key="PostItButtonBrush" Color="#FFE066" />
    <SolidColorBrush x:Key="PostItButtonHoverBrush" Color="#FFD633" />
    <SolidColorBrush x:Key="PostItButtonPressedBrush" Color="#F0C419" />
    <SolidColorBrush x:Key="PostItTextBrush" Color="#5C4F1A" />

    <Style x:Key="PostItButtonBaseStyle" TargetType="{x:Type ButtonBase}">
        <Setter Property="Background" Value="{StaticResource PostItButtonBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource PostItBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Foreground" Value="{StaticResource PostItTextBrush}" />
        <Setter Property="Padding" Value="8,4" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ButtonBase}">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="6">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Background" Value="{StaticResource PostItButtonHoverBrush}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter Property="Background" Value="{StaticResource PostItButtonPressedBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType="{x:Type Button}" BasedOn="{StaticResource PostItButtonBaseStyle}" />
    <Style TargetType="{x:Type ToggleButton}" BasedOn="{StaticResource PostItButtonBaseStyle}">
        <Style.Triggers>
            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Background" Value="{StaticResource PostItButtonPressedBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>
</Window.Resources>
```

`IsChecked` only exists on `ToggleButton`, not `ButtonBase` — that's why its trigger lives in the `ToggleButton`-targeted `Style.Triggers`, not inside the shared `ControlTemplate.Triggers` (which targets `ButtonBase` and can only reference properties common to both `Button` and `ToggleButton`). The two implicit styles (keyed by exact `TargetType`, no `x:Key`) are what makes every existing `Button`/`ToggleButton` in the window pick this up automatically — an implicit style keyed `TargetType="ButtonBase"` alone would *not* apply to either, since WPF resolves implicit styles by an element's exact runtime type.

- [ ] **Step 2: Apply the palette to the window chrome and panels**

On the `Window` element, add `Background="{DynamicResource PostItBackgroundBrush}"` — **`DynamicResource`, not `StaticResource`, here specifically**: an attribute on the `Window`'s own start tag is applied before its `Window.Resources` property element is parsed, so a `StaticResource` reference to a brush defined in that same dictionary is a forward reference that throws `XamlParseException` out of `InitializeComponent()` on every launch (confirmed by a final review of this plan's first implementation, via the compiled BAML's record ordering). `DynamicResource` defers the lookup until the value is actually needed, after the whole tree — `Window.Resources` included — has loaded, which is the standard fix for this exact ordering problem. Every other `StaticResource` reference in this file is fine, since it's either later in the same dictionary or in content parsed after `Window.Resources` closes — this is the one place order bites. On the toolbar `StackPanel`, add `Background="{StaticResource PostItToolbarBrush}"`. Change the divider `Border`'s `Background` from `#FFBEBEBE` to `{StaticResource PostItBorderBrush}`.

- [ ] **Step 3: Restyle the editor and preview surfaces**

On `EditorTextBox`: add `Background="{StaticResource PostItBackgroundBrush}"`, `Foreground="{StaticResource PostItTextBrush}"`, `BorderThickness="0"` (a boxed-in textbox border would break the "this is the note" illusion). On `PreviewScrollViewer`: add `Background="{StaticResource PostItBackgroundBrush}"`.

- [ ] **Step 4: Apply the background to the rendered preview's `FlowDocument`**

Markdig.Wpf creates a fresh `FlowDocument` on every `Markdown` assignment (the same reason `ApplyPreviewFontSize`/`ApplyPreviewFonts` already exist as post-render steps), so the `ScrollViewer`'s own `Background` alone may not be visible if that `FlowDocument` paints its own opaque background on top. Add, in `MainWindow.xaml.cs`:

```csharp
private void ApplyPreviewBackground()
{
    if (PreviewViewer.Document == null)
    {
        return;
    }

    PreviewViewer.Document.Background = (Brush)FindResource("PostItBackgroundBrush");
}
```

Call `ApplyPreviewBackground()` inside `RenderPreview()`, alongside the existing `ApplyPreviewFontSize()`/`ApplyPreviewFonts()` calls.

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Manually verify, on the target Windows machine**

- Launch the app and confirm it actually starts (no crash dialog) before checking anything else — see Step 2's `DynamicResource` note for why this specifically needs confirming here, not assuming from a successful build.
- The toolbar, its buttons, and the editor are all the pale-yellow palette — not just the editor text area.
- Click `Preview`/`Always on Top`: each toggle still visibly shows a distinct checked/pressed state, not just a flat unchanging button.
- Hover and click any button: hover and pressed states are visibly distinct from the resting color.
- Toggle to Preview mode: the rendered preview's background is the same pale yellow as the editor, with no leftover white rectangle showing through from Markdig.Wpf's own default. If it does show through, that confirms Step 4's `ApplyPreviewBackground()` is load-bearing (it should already fix it) — if it still doesn't, this needs further investigation before Task 2.

- [ ] **Step 7: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: restyle MainWindow to a post-it palette"
```

---

## Task 2: Custom Window Chrome

**Files:**
- Modify: `Scratchpad/MainWindow.xaml`
- Modify: `Scratchpad/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `PostItToolbarBrush` resource key (Task 1) — moved from the toolbar `StackPanel` to the new wrapping `Grid`; the `Button` implicit style (Task 1) — applies automatically to the new hide button, no extra wiring needed.

- [ ] **Step 1: Replace the native title bar**

On the `Window` element: add `xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"`, add `WindowStyle="None"` (keep `ResizeMode` at its current default, `CanResize`), and add as a child:

```xml
<shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="0" ResizeBorderThickness="6" GlassFrameThickness="0" UseAeroCaptionButtons="False" />
</shell:WindowChrome.WindowChrome>
```

`CaptionHeight="0"` is deliberate: setting it to the toolbar's actual height would mark that area a native caption region, which brings back double-click-to-maximize and Aero-snap "for free" — both explicitly out of scope. Drag and resize are rebuilt by hand instead (Steps 2-3).

- [ ] **Step 2: Wrap the toolbar in a `Grid` with a drag-space column and the hide button**

Wrap the existing toolbar `StackPanel` (unchanged — same children, same order) in a `Grid`: move `DockPanel.Dock="Top"` and the `Background="{StaticResource PostItToolbarBrush}"` from the `StackPanel` onto this new `Grid` (the `StackPanel` no longer needs its own `Background` — it's transparent, letting the `Grid`'s show through across the whole row, drag-space and hide button included). Add `MouseLeftButtonDown="Toolbar_MouseLeftButtonDown"` to the `Grid`. Three columns: `Auto` (the `StackPanel`), `*` (empty — this is the drag space), `Auto` (the new hide button):

```xml
<Grid DockPanel.Dock="Top" Background="{StaticResource PostItToolbarBrush}" MouseLeftButtonDown="Toolbar_MouseLeftButtonDown">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <StackPanel Grid.Column="0" Orientation="Horizontal">
        <!-- existing PreviewToggleButton, AlwaysOnTopToggleButton, divider, ZoomOutButton, ZoomInButton, SettingsButton, unchanged -->
    </StackPanel>
    <Button x:Name="HideButton" Grid.Column="2" Content="X" Click="HideButton_Click" Margin="2,4" MinWidth="28" Focusable="False" />
</Grid>
```

`HideButton` gets `Focusable="False"` for the same reason `ZoomOutButton`/`ZoomInButton`/`SettingsButton` already do — a past review on this project caught that a focusable toolbar button steals keyboard focus from the editor on click.

- [ ] **Step 3: Implement dragging and hiding in `MainWindow.xaml.cs`**

```csharp
private void Toolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    DragMove();
}

private void HideButton_Click(object sender, RoutedEventArgs e)
{
    Close();
}
```

`Toolbar_MouseLeftButtonDown` is wired to the bubbling `MouseLeftButtonDown` event, not `PreviewMouseLeftButtonDown` — a click on any button inside the `StackPanel` (or the hide button) is marked `Handled` by that button's own click-handling before the event reaches the `Grid`, so this handler only ever fires for clicks on genuinely empty toolbar space. `HideButton_Click` calls `Close()` directly — the same method the native X button already calls, which `Window_Closing` already intercepts to hide to the tray (`MainWindow.xaml.cs`, existing `Window_Closing`/`_isExiting` logic) — no new hide-vs-exit logic needed.

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build Scratchpad -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manually verify, on the target Windows machine**

- Launch the app and confirm it actually starts — no crash dialog. This sounds trivial, but a final review of this plan's first implementation found a `Window.Background` forward-reference that crashed `InitializeComponent()` on every launch (see Step 1's `DynamicResource` note); nothing short of actually running the app would have caught it.
- Launch the app: no native title bar is visible at all (no Windows-drawn minimize/maximize/close buttons, no Windows-drawn title text).
- Click and drag empty toolbar space (not a button): the window moves, the same as dragging a native title bar.
- Click any toolbar button (`Preview`, `Always on Top`, zoom, `Settings`): it activates normally and does **not** also start dragging the window.
- Drag the window to the top edge of the screen and release: confirm it does **not** maximize and get stuck there (Aero Snap still runs inside `DragMove()`'s native move loop even with no native title bar; `Window_StateChanged` forces it back to `Normal` if it does).
- Click the new hide button: the window hides to the tray — confirm from the tray icon's "Show" (or double-click) that it reappears with its content intact, exactly like today's native X button.
- Drag an edge or corner of the window: it resizes, the same as with the native border.
- Type in the editor, click the hide button, then a toolbar button, then drag the window — confirm keyboard focus in the editor is never unexpectedly lost by any of these interactions.
- If the laptop is undocked/redocked (switching which single display is active) between runs, confirm the window still reopens clamped onto the visible screen, as it already did before this change (`WindowPlacement.Clamp`, unaffected by this plan).

- [ ] **Step 6: Commit**

```bash
git add Scratchpad/MainWindow.xaml Scratchpad/MainWindow.xaml.cs
git commit -m "feat: replace native title bar with custom chrome"
```
