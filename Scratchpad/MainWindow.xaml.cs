using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using Scratchpad.Core;

namespace Scratchpad
{
    public partial class MainWindow : Window
    {
        private readonly DocumentStore _documentStore = new DocumentStore(DocumentStore.DefaultFilePath);
        private readonly WindowPlacementStore _placementStore = new WindowPlacementStore(WindowPlacementStore.DefaultFilePath);
        private readonly FontSizeStore _fontSizeStore = new FontSizeStore(FontSizeStore.DefaultFilePath);
        private FontSizeSettings _fontSizeSettings;
        private readonly FontFamilyStore _fontFamilyStore = new FontFamilyStore(FontFamilyStore.DefaultFilePath);
        private FontFamilySettings _fontFamilySettings;
        private readonly StartupWindowStore _startupWindowStore = new StartupWindowStore(StartupWindowStore.DefaultFilePath);
        private StartupWindowSettings _startupWindowSettings;
        private readonly DateFormatStore _dateFormatStore = new DateFormatStore(DateFormatStore.DefaultFilePath);
        private DateFormatSettings _dateFormatSettings;
        private readonly PeekStore _peekStore = new PeekStore(PeekStore.DefaultFilePath);
        private PeekSettings _peekSettings;
        private readonly HotkeyStore _hotkeyStore = new HotkeyStore(HotkeyStore.DefaultFilePath);
        private readonly GlobalHotkey _globalHotkey;
        private readonly DispatcherTimer _peekTimer;
        private enum PeekState { Idle, SlidingOut, Away, SlidingBack }
        // TimeSpan.Zero turns the slide off: the note then jumps off-screen and back.
        private static readonly TimeSpan PeekSlideDuration = TimeSpan.FromMilliseconds(200);
        private PeekState _peekState;
        private double _peekHomeTop;
        private double _slideFrom;
        private double _slideTo;
        private readonly Stopwatch _slideClock = new Stopwatch();
        private bool _isSlideFrameAttached;
        private SettingsWindow _settingsWindow;
        private readonly PreviewNavigator _previewNavigator;
        private readonly ThemeManager _themeManager;
        private readonly DebouncedAutosaveCoordinator _autosave;
        private bool _isExiting;

        public MainWindow(ThemeManager themeManager)
        {
            InitializeComponent();
            _themeManager = themeManager;
            // Brushes elsewhere follow the swap on their own (DynamicResource); the preview's
            // dark overrides are per-element local values, so they need re-walking.
            _themeManager.ThemeChanged += theme =>
            {
                if (PreviewToggleButton.IsChecked == true)
                {
                    ApplyPreviewTheme();
                }
                _settingsWindow?.ApplyTheme(theme);
            };
            _autosave = new DebouncedAutosaveCoordinator(_documentStore.Save, TimeSpan.FromMilliseconds(500));
            // Must match Scratchpad.Core.TaskListToggler's pipeline so checkbox positions agree
            // between what's rendered here and what gets toggled there.
            PreviewViewer.Pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
            _previewNavigator = new PreviewNavigator(PreviewScrollViewer, PreviewViewer);

            // Loaded here, not in Window_Loaded: by default this app starts hidden in the tray
            // (App.xaml.cs only calls Show() when "Show the window when Scratchpad starts" is
            // on), so Window_Loaded never runs on the common exit-without-opening path. Loading in the constructor means _fontSizeSettings
            // always reflects the persisted value by the time Window_Closing saves it back,
            // instead of silently overwriting a custom size with FontSizing.Default.
            _fontSizeSettings = _fontSizeStore.Load();
            ApplyEditorFontSize();

            // Same reasoning as font size above: loaded in the constructor, not Window_Loaded,
            // and validated against what's actually installed since FontFamilyStore.Load() (in
            // Scratchpad.Core, with no font API) can only guarantee non-empty strings, not that
            // the named font still exists on this machine.
            _fontFamilySettings = ValidateAgainstInstalledFonts(_fontFamilyStore.Load());
            ApplyEditorFontFamily();

            _startupWindowSettings = _startupWindowStore.Load();
            _dateFormatSettings = _dateFormatStore.Load();
            _peekSettings = _peekStore.Load();

            _peekTimer = new DispatcherTimer();
            _peekTimer.Tick += OnPeekTimerTick;

            _globalHotkey = new GlobalHotkey();
            _globalHotkey.Pressed += OnGlobalHotkeyPressed;
        }

        // Read by App.OnStartup once everything else is in place; changing it in Settings only
        // affects the next start.
        public bool ShowWindowOnStartup => _startupWindowSettings.ShowWindowOnStartup;

        private static FontFamilySettings ValidateAgainstInstalledFonts(FontFamilySettings settings)
        {
            // Enumerating every installed font (FontCatalog's first touch) resolves a
            // GlyphTypeface per family and is slow on a machine with a large font set. Skipping
            // it whenever nothing has been configured away from the defaults keeps that cost out
            // of the overwhelmingly common startup path — notably ahead of the single-instance
            // listener in App.xaml.cs, which a slow constructor would otherwise delay.
            if (settings.IsDefault())
            {
                return settings;
            }

            var editorFontFamily = FontCatalog.MonospaceFamilies.Any(f => f.Source == settings.EditorFontFamily)
                ? settings.EditorFontFamily
                : FontFamilyStore.DefaultEditorFontFamily;
            var previewNormalFontFamily = FontCatalog.NormalTextFamilies.Any(f => f.Source == settings.PreviewNormalFontFamily)
                ? settings.PreviewNormalFontFamily
                : null;
            var previewCodeFontFamily = FontCatalog.MonospaceFamilies.Any(f => f.Source == settings.PreviewCodeFontFamily)
                ? settings.PreviewCodeFontFamily
                : null;

            return new FontFamilySettings
            {
                EditorFontFamily = editorFontFamily,
                PreviewNormalFontFamily = previewNormalFontFamily,
                PreviewCodeFontFamily = previewCodeFontFamily
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Text = _documentStore.Load();

            var requested = _placementStore.Load();
            var clamped = WindowPlacement.Clamp(requested, GetScreenBoundsInDeviceIndependentUnits());
            Left = clamped.Left;
            Top = clamped.Top;
            Width = clamped.Width;
            Height = clamped.Height;
        }

        private IReadOnlyList<WindowBounds> GetScreenBoundsInDeviceIndependentUnits()
        {
            // Converted using this window's current-monitor DPI transform. On a mixed-DPI
            // multi-monitor setup this is an approximation rather than an exact per-monitor
            // conversion, but it keeps every screen in the same coordinate space as Left/Top.
            var transform = Matrix.Identity;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                transform = source.CompositionTarget.TransformFromDevice;
            }

            return System.Windows.Forms.Screen.AllScreens.Select(screen =>
            {
                var topLeft = transform.Transform(new Point(screen.Bounds.Left, screen.Bounds.Top));
                var bottomRight = transform.Transform(new Point(screen.Bounds.Right, screen.Bounds.Bottom));
                return new WindowBounds
                {
                    Left = topLeft.X,
                    Top = topLeft.Y,
                    Width = bottomRight.X - topLeft.X,
                    Height = bottomRight.Y - topLeft.Y
                };
            }).ToList();
        }

        private void EditorTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _autosave.NotifyChanged(EditorTextBox.Text);
        }

        private void PreviewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            SetPreviewMode(PreviewToggleButton.IsChecked == true);
        }

        private void SetPreviewMode(bool showPreview)
        {
            if (showPreview)
            {
                PreviewToggleButton.IsChecked = true;
                var editorTop = EditorTextBox.IsVisible ? EditorTopCharacterIndex() : -1;
                RenderPreview();
                EditorTextBox.Visibility = Visibility.Collapsed;
                PreviewScrollViewer.Visibility = Visibility.Visible;
                ScrollPreviewToSource(editorTop);
            }
            else
            {
                SwitchToEditorAt(null);
            }
        }

        /// <summary>
        /// Shows the editor with the block that was at the top of the preview at the top of the
        /// editor, so switching keeps your place. The caret goes to caretIndex when given (a
        /// double-click), else to the start of that top block.
        /// </summary>
        private void SwitchToEditorAt(int? caretIndex)
        {
            PreviewToggleButton.IsChecked = false;
            var scrollTarget = PreviewScrollViewer.IsVisible ? TopPreviewBlockStart() : null;
            var caret = caretIndex ?? scrollTarget;

            EditorTextBox.Visibility = Visibility.Visible;
            PreviewScrollViewer.Visibility = Visibility.Collapsed;
            EditorTextBox.Focus();

            // Measuring is best-effort and can't be exercised outside Windows: if anything here
            // fails, the switch has still happened and the editor just stays where it was.
            try
            {
                var length = EditorTextBox.Text.Length;
                if (caret != null)
                {
                    EditorTextBox.CaretIndex = Math.Max(0, Math.Min(caret.Value, length));
                }
                if (scrollTarget != null)
                {
                    ScrollEditorToCharacter(Math.Max(0, Math.Min(scrollTarget.Value, length)));
                }
                if (caretIndex != null && !IsCaretOnScreen())
                {
                    ScrollEditorToCharacter(EditorTextBox.CaretIndex);
                }
            }
            catch (Exception)
            {
            }
        }

        private void PreviewScrollViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // A task checkbox toggles on each click, so a double-click on one is two toggles, not
            // a request to edit. Double-clicking the scrollbar isn't either.
            if (IsWithin<CheckBox>(e.OriginalSource) || IsWithin<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource))
            {
                return;
            }

            e.Handled = true;
            try
            {
                SwitchToEditorAt(ClickedSourceOffset(e));
            }
            catch (Exception)
            {
                SwitchToEditorAt(null);
            }
        }

        // Where in the source a double-click in the preview landed: the clicked character when it
        // can be found, else the start of the clicked block, else the end of the note for space
        // below the last block. Null when the preview and source don't line up.
        private int? ClickedSourceOffset(MouseButtonEventArgs e)
        {
            var text = EditorTextBox.Text;
            var units = _previewNavigator.GetUnits();
            var map = PreviewSourceMap.Build(text);
            if (units.Count != map.Count || units.Count == 0)
            {
                return null;
            }

            Point? point = _previewNavigator.ToExtent(e.GetPosition(PreviewScrollViewer));
            var index = -1;
            for (var element = e.OriginalSource as DependencyObject; element is ContentElement; element = LogicalTreeHelper.GetParent(element))
            {
                if (element is Block block)
                {
                    index = IndexOfUnit(units, block);
                    if (index >= 0)
                    {
                        break;
                    }
                }
            }

            if (index < 0)
            {
                if (point == null)
                {
                    return null;
                }
                index = _previewNavigator.UnitIndexAtExtentY(units, point.Value.Y);
                if (index == -1)
                {
                    return text.Length;
                }
                if (index < 0)
                {
                    return null;
                }
            }

            if (e.OriginalSource is Run run && point != null)
            {
                var offsetInRun = _previewNavigator.OffsetInRun(run, point.Value);
                var lineText = CodeLineAround(run, offsetInRun, out var offsetInLine);
                return lineText != null
                    ? PreviewSourceMap.LocateInBlock(text, map[index], lineText, offsetInLine)
                    : PreviewSourceMap.LocateInBlock(text, map[index], run.Text, offsetInRun);
            }
            return map[index].Start;
        }

        // A highlighted code line is several Runs, and a short one ("i") would be found at its
        // first occurrence anywhere in the block, so search for the whole line instead, as before
        // highlighting split it. Null when the run isn't in a code block.
        private string CodeLineAround(Run run, int offsetInRun, out int offsetInLine)
        {
            offsetInLine = offsetInRun;
            var codeBlockStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeBlockStyleKey) as Style;
            if (!(run.Parent is Paragraph paragraph) || codeBlockStyle == null || !ReferenceEquals(paragraph.Style, codeBlockStyle))
            {
                return null;
            }

            var line = new System.Text.StringBuilder();
            var foundRun = false;
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is LineBreak)
                {
                    if (foundRun)
                    {
                        break;
                    }
                    line.Clear();
                }
                else if (inline is Run piece)
                {
                    if (ReferenceEquals(piece, run))
                    {
                        foundRun = true;
                        offsetInLine = line.Length + offsetInRun;
                    }
                    line.Append(piece.Text);
                }
            }

            return foundRun ? line.ToString() : null;
        }

        private static int IndexOfUnit(IReadOnlyList<Block> units, Block block)
        {
            for (var i = 0; i < units.Count; i++)
            {
                if (ReferenceEquals(units[i], block))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsWithin<T>(object source) where T : DependencyObject
        {
            for (var element = source as DependencyObject; element != null;
                 element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
            {
                if (element is T)
                {
                    return true;
                }
            }
            return false;
        }

        // The source offset of the block at the top of the preview, or null when the rendered
        // blocks don't line up with the source map (then nothing moves).
        private int? TopPreviewBlockStart()
        {
            try
            {
                var units = _previewNavigator.GetUnits();
                var map = PreviewSourceMap.Build(EditorTextBox.Text);
                if (units.Count != map.Count)
                {
                    return null;
                }
                var index = _previewNavigator.TopUnitIndex(units);
                return index >= 0 ? map[index].Start : (int?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ScrollPreviewToSource(int sourceOffset)
        {
            if (sourceOffset < 0)
            {
                return;
            }

            try
            {
                PreviewScrollViewer.UpdateLayout();
                var units = _previewNavigator.GetUnits();
                var map = PreviewSourceMap.Build(EditorTextBox.Text);
                if (units.Count != map.Count)
                {
                    return;
                }
                var index = PreviewSourceMap.BlockIndexAt(map, sourceOffset);
                if (index >= 0)
                {
                    _previewNavigator.ScrollUnitToTop(units[index]);
                }
            }
            catch (Exception)
            {
            }
        }

        private int EditorTopCharacterIndex()
        {
            try
            {
                var line = EditorTextBox.GetFirstVisibleLineIndex();
                return line < 0 ? -1 : EditorTextBox.GetCharacterIndexFromLineIndex(line);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private bool IsCaretOnScreen()
        {
            EditorTextBox.UpdateLayout();
            var rect = EditorTextBox.GetRectFromCharacterIndex(EditorTextBox.CaretIndex);
            // The rect is relative to the whole TextBox, padding included, so the visible text
            // ends at the bottom padding rather than at ViewportHeight.
            return !rect.IsEmpty && rect.Top >= 0 && rect.Bottom <= EditorTextBox.ActualHeight - EditorTextBox.Padding.Bottom;
        }

        private void ScrollEditorToCharacter(int index)
        {
            EditorTextBox.UpdateLayout();
            var rect = EditorTextBox.GetRectFromCharacterIndex(index);
            if (!rect.IsEmpty)
            {
                // The rect includes the text padding; landing the line just below it, not under it.
                EditorTextBox.ScrollToVerticalOffset(Math.Max(0, EditorTextBox.VerticalOffset + rect.Top - EditorTextBox.Padding.Top));
            }
            else
            {
                EditorTextBox.ScrollToLine(EditorTextBox.GetLineIndexFromCharacterIndex(index));
            }
        }

        private void RenderPreview()
        {
            PreviewViewer.Markdown = EditorTextBox.Text;
            HighlightCodeBlocks();
            ApplyPreviewFontSize();
            ApplyPreviewFonts();
            ApplyPreviewTheme();
            ApplyPreviewBackground();
            WireTaskListCheckBoxes();
        }

        // Markdig.Wpf draws a fenced block as one code-style Paragraph of Runs (one per line) and
        // LineBreaks, and drops the language tag, so the tag comes from the source map, paired
        // unit-for-unit as double-click does. Each tagged block's inlines are swapped for one Run
        // per token; coloured runs hold a resource reference, so a theme switch recolours them,
        // and plain runs keep inheriting the paragraph ink ApplyPreviewTheme sets. The text is
        // rebuilt from the current inlines, not the source, so this is safe to repeat: Markdig.Wpf
        // keeps the same FlowDocument for unchanged text, so a re-shown preview arrives already
        // highlighted.
        private void HighlightCodeBlocks()
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            var units = _previewNavigator.GetUnits();
            var map = PreviewSourceMap.Build(EditorTextBox.Text);
            if (units.Count != map.Count)
            {
                return;
            }

            var codeBlockStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeBlockStyleKey) as Style;
            if (codeBlockStyle == null)
            {
                return;
            }

            for (var i = 0; i < units.Count; i++)
            {
                if (!(units[i] is Paragraph paragraph) || !ReferenceEquals(paragraph.Style, codeBlockStyle)
                    || !CodeHighlighter.IsSupported(map[i].CodeLanguage))
                {
                    continue;
                }

                try
                {
                    HighlightCodeBlock(paragraph, map[i].CodeLanguage);
                }
                catch (Exception)
                {
                    // Leave this block as Markdig.Wpf drew it; the rest still get coloured.
                }
            }
        }

        private static void HighlightCodeBlock(Paragraph paragraph, string language)
        {
            var text = new System.Text.StringBuilder();
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is Run run)
                {
                    text.Append(run.Text);
                }
                else if (inline is LineBreak)
                {
                    text.Append('\n');
                }
                else
                {
                    return;
                }
            }

            var tokens = CodeHighlighter.Tokenize(text.ToString(), language);
            if (tokens == null)
            {
                return;
            }

            var inlines = new List<Inline>();
            foreach (var token in tokens)
            {
                var pieces = token.Text.Split('\n');
                for (var p = 0; p < pieces.Length; p++)
                {
                    if (p > 0)
                    {
                        inlines.Add(new LineBreak());
                    }
                    if (pieces[p].Length == 0)
                    {
                        continue;
                    }

                    var run = new Run(pieces[p]);
                    if (token.Kind != CodeTokenKind.Plain)
                    {
                        run.SetResourceReference(TextElement.ForegroundProperty, "PreviewCode" + token.Kind + "Brush");
                    }
                    inlines.Add(run);
                }
            }

            paragraph.Inlines.Clear();
            paragraph.Inlines.AddRange(inlines);
        }

        // Markdig.Wpf creates a fresh FlowDocument whenever the Markdown text changes (the same reason
        // ApplyPreviewFontSize/ApplyPreviewFonts already exist as post-render steps) — the
        // ScrollViewer's own Background alone isn't visible if that FlowDocument paints its own
        // opaque background on top. Each new document also brings automatic page padding, which
        // would add to PreviewViewer's NoteTextPadding margin, so it's zeroed here too.
        private void ApplyPreviewBackground()
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            // A resource reference rather than a one-off lookup, so a theme switch while the
            // preview is showing re-colours it too.
            PreviewViewer.Document.SetResourceReference(FlowDocument.BackgroundProperty, "PostItBackgroundBrush");
            PreviewViewer.Document.PagePadding = new Thickness(0);
        }

        private void AdjustFontSize(int direction)
        {
            if (PreviewToggleButton.IsChecked == true)
            {
                _fontSizeSettings.PreviewFontSize = direction > 0
                    ? FontSizing.Increase(_fontSizeSettings.PreviewFontSize)
                    : FontSizing.Decrease(_fontSizeSettings.PreviewFontSize);
                ApplyPreviewFontSize();
            }
            else
            {
                _fontSizeSettings.EditorFontSize = direction > 0
                    ? FontSizing.Increase(_fontSizeSettings.EditorFontSize)
                    : FontSizing.Decrease(_fontSizeSettings.EditorFontSize);
                ApplyEditorFontSize();
            }
        }

        private void ResetFontSize()
        {
            if (PreviewToggleButton.IsChecked == true)
            {
                _fontSizeSettings.PreviewFontSize = FontSizing.Default;
                ApplyPreviewFontSize();
            }
            else
            {
                _fontSizeSettings.EditorFontSize = FontSizing.Default;
                ApplyEditorFontSize();
            }
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-1);
        private void ZoomInButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(1);

        // Wired to the bubbling MouseLeftButtonDown, not PreviewMouseLeftButtonDown — a click on
        // any toolbar button is marked Handled by that button's own click-handling before the
        // event reaches this Grid, so this only ever fires for clicks on genuinely empty space.
        private void Toolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // DragMove() throws if the left button is no longer down by the time this handler
            // runs (capture races, double-click sequences, injected input) — guard rather than
            // trust the event always reflects the button's current state.
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            DragMove();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // DragMove() enters the OS's native move loop, which still implements Aero Snap even
            // with the native title bar gone — dragging to a screen edge can maximize the window.
            // With no restore control in this custom chrome (no maximize button, and DragMove()
            // itself no-ops while maximized), that would be a dead end with no way back except
            // the taskbar icon's right-click menu or quitting.
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            // Clicking the taskbar button of the (still active) off-screen note minimises it;
            // the user meant "bring it back", so do that instead.
            if (WindowState == WindowState.Minimized && _peekState != PeekState.Idle)
            {
                WindowState = WindowState.Normal;
                CancelPeek();
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

        // The tray's Settings… item. Settings centres on this window, so show it first — and
        // defer opening until after Window_Loaded, which on the first Show() restores the saved
        // position only after Show() returns; otherwise Settings centres on Windows' default spot.
        public void OpenSettingsFromTray()
        {
            ShowAndActivate();
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(OpenSettings));
        }

        private void OpenSettings()
        {
            // Only ever one Settings window: a second click brings the existing one forward.
            if (_settingsWindow != null)
            {
                if (_settingsWindow.WindowState == WindowState.Minimized)
                {
                    _settingsWindow.WindowState = WindowState.Normal;
                }
                _settingsWindow.Activate();
                return;
            }

            var settingsWindow = new SettingsWindow(_fontFamilySettings, _themeManager.Preference, _themeManager.Current,
                _startupWindowSettings.ShowWindowOnStartup, _dateFormatSettings.Format, _peekSettings.Seconds,
                FontCatalog.MonospaceFamilies, FontCatalog.NormalTextFamilies);
            // Without an Owner, Windows places the window at its default cascade position
            // (top-left), unrelated to wherever the user has parked Scratchpad. Owning it also
            // keeps it above an always-on-top main window and hides it alongside the main window.
            settingsWindow.Owner = this;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingsWindow.SettingsChanged += OnFontFamilySettingsChanged;
            settingsWindow.ResetRequested += OnResetRequested;
            settingsWindow.ShowWindowOnStartupChanged += show =>
            {
                _startupWindowSettings = new StartupWindowSettings { ShowWindowOnStartup = show };
                _startupWindowStore.Save(_startupWindowSettings);
            };
            settingsWindow.DateFormatChanged += format =>
            {
                _dateFormatSettings = new DateFormatSettings { Format = format };
                _dateFormatStore.Save(_dateFormatSettings);
            };
            settingsWindow.PeekSecondsChanged += seconds =>
            {
                _peekSettings = new PeekSettings { Seconds = seconds };
                _peekStore.Save(_peekSettings);
            };
            // Deferred: applying swaps the palette, which re-templates the very combo box whose
            // SelectionChanged is still on the stack — let that handler finish first.
            settingsWindow.ThemePreferenceChanged += preference =>
                Dispatcher.BeginInvoke(new Action(() => _themeManager.SetPreference(preference)));
            settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow = settingsWindow;
            settingsWindow.Show();
        }

        private void OnFontFamilySettingsChanged(FontFamilySettings settings)
        {
            _fontFamilySettings = settings;
            ApplyEditorFontFamily();
            if (PreviewToggleButton.IsChecked == true)
            {
                ApplyPreviewFonts();
            }
            _fontFamilyStore.Save(_fontFamilySettings);
        }

        // Leaves window placement, always-on-top and preview mode alone — fonts, font sizes,
        // theme, show-on-startup, the date format and the peek time reset. Font sizes are saved here rather than waiting for the usual on-hide save.
        private void OnResetRequested()
        {
            _fontSizeSettings = FontSizeStore.Defaults;
            ApplyEditorFontSize();
            ApplyPreviewFontSize();
            _fontSizeStore.Save(_fontSizeSettings);

            _fontFamilySettings = FontFamilyStore.Defaults;
            ApplyEditorFontFamily();
            if (PreviewToggleButton.IsChecked == true)
            {
                ApplyPreviewFonts();
            }
            _fontFamilyStore.Save(_fontFamilySettings);

            _themeManager.SetPreference(ThemeStore.Defaults.Preference);

            _startupWindowSettings = StartupWindowStore.Defaults;
            _startupWindowStore.Save(_startupWindowSettings);

            _dateFormatSettings = DateFormatStore.Defaults;
            _dateFormatStore.Save(_dateFormatSettings);

            _peekSettings = PeekStore.Defaults;
            _peekStore.Save(_peekSettings);

            _settingsWindow?.ShowSettings(_fontFamilySettings, _themeManager.Preference, _startupWindowSettings.ShowWindowOnStartup,
                _dateFormatSettings.Format, _peekSettings.Seconds);
        }

        private void ApplyEditorFontFamily()
        {
            // The Settings window can hand back a null EditorFontFamily (its combo box opens
            // with no selection whenever the loaded/validated value isn't in FontCatalog's own
            // list), and new FontFamily(null) throws — guard here rather than trust every caller.
            var fontFamily = string.IsNullOrEmpty(_fontFamilySettings.EditorFontFamily)
                ? FontFamilyStore.DefaultEditorFontFamily
                : _fontFamilySettings.EditorFontFamily;
            EditorTextBox.FontFamily = new FontFamily(fontFamily);
        }

        private void ApplyPreviewFonts()
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            ApplyPreviewFonts(PreviewViewer.Document.Blocks);
        }

        // A null Preview*FontFamily means "no override" — clear any FontFamily an earlier pass
        // stamped on the element (Markdig.Wpf can keep the same FlowDocument across renders, so
        // a previous choice would otherwise linger), letting Markdig.Wpf's own default render
        // exactly as it does for anyone who's never opened Settings. Mirrors FindTaskListCheckBoxes's recursion shape rather than
        // inventing a new tree-walk pattern. Style-reference comparison (not a resource-override
        // mechanism) is used purely to classify each element — stamping FontFamily as a local
        // value always wins regardless of what Markdig.Wpf's own styles do internally, sidestepping
        // any need to know their exact TargetType, which this sandbox has no way to verify.
        private void ApplyPreviewFonts(IEnumerable<Block> blocks)
        {
            var codeBlockStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeBlockStyleKey) as Style;
            var codeStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeStyleKey) as Style;

            foreach (var block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    var isCodeBlock = codeBlockStyle != null && ReferenceEquals(paragraph.Style, codeBlockStyle);
                    var paragraphFontFamily = isCodeBlock ? _fontFamilySettings.PreviewCodeFontFamily : _fontFamilySettings.PreviewNormalFontFamily;
                    SetOrClearFontFamily(paragraph, paragraphFontFamily);

                    if (!isCodeBlock && codeStyle != null)
                    {
                        foreach (var run in FindRuns(paragraph.Inlines))
                        {
                            if (ReferenceEquals(run.Style, codeStyle))
                            {
                                SetOrClearFontFamily(run, _fontFamilySettings.PreviewCodeFontFamily);
                            }
                        }
                    }
                }
                else if (block is Section section)
                {
                    ApplyPreviewFonts(section.Blocks);
                }
                else if (block is List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        ApplyPreviewFonts(item.Blocks);
                    }
                }
            }
        }

        // Markdig.Wpf's own styles colour the preview for a white page (dark text, light-grey
        // code and quote borders, system-blue links). In dark, stamp resource references onto
        // each element — local values beat style setters, and references follow later palette
        // swaps. In light, clear exactly those values so Markdig.Wpf's look returns untouched.
        // Classification mirrors ApplyPreviewFonts (style-reference comparison).
        private void ApplyPreviewTheme()
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            var dark = _themeManager.Current == AppTheme.Dark;
            SetOrClearReference(PreviewViewer.Document, TextElement.ForegroundProperty, "PostItTextBrush", dark);
            ApplyPreviewTheme(PreviewViewer.Document.Blocks, dark, "PostItTextBrush");
        }

        // textBrushKey is the ink for this level: body text, or the dimmer quote ink inside a
        // block quote (light mode's quote style greys its text; this keeps that distinction).
        private void ApplyPreviewTheme(IEnumerable<Block> blocks, bool dark, string textBrushKey)
        {
            var codeBlockStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeBlockStyleKey) as Style;
            var codeStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.CodeStyleKey) as Style;
            var quoteStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.QuoteBlockStyleKey) as Style;
            var tableHeaderStyle = PreviewViewer.TryFindResource(Markdig.Wpf.Styles.TableHeaderStyleKey) as Style;

            foreach (var block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    // Every paragraph, not just the document: heading styles may set their own
                    // Foreground, which a document-level value wouldn't override.
                    SetOrClearReference(paragraph, TextElement.ForegroundProperty, textBrushKey, dark);

                    if (codeBlockStyle != null && ReferenceEquals(paragraph.Style, codeBlockStyle))
                    {
                        SetOrClearReference(paragraph, TextElement.BackgroundProperty, "PreviewCodeBackgroundBrush", dark);
                    }

                    foreach (var inline in FindInlines(paragraph.Inlines))
                    {
                        if (inline is Hyperlink)
                        {
                            SetOrClearReference(inline, TextElement.ForegroundProperty, "PreviewLinkBrush", dark);
                        }
                        else if (inline is Run && codeStyle != null && ReferenceEquals(inline.Style, codeStyle))
                        {
                            SetOrClearReference(inline, TextElement.ForegroundProperty, textBrushKey, dark);
                            SetOrClearReference(inline, TextElement.BackgroundProperty, "PreviewCodeBackgroundBrush", dark);
                        }
                        else if (inline is InlineUIContainer container && container.Child is CheckBox checkBox)
                        {
                            // Markdig.Wpf gives each task-list checkbox an explicit style
                            // (TaskListStyleKey), which blocks any implicit CheckBox style — so swap
                            // the style reference itself, and in light restore Markdig's (not
                            // ClearValue, which would drop its margin and shift the layout).
                            checkBox.SetResourceReference(StyleProperty, dark
                                ? (object)"PostItTaskListCheckBoxStyle"
                                : Markdig.Wpf.Styles.TaskListStyleKey);
                        }
                        else if (inline is InlineUIContainer ruleContainer && ruleContainer.Child is System.Windows.Shapes.Line rule)
                        {
                            // A thematic break (---): Markdig.Wpf draws a black Line inside a
                            // paragraph, all but invisible on dark paper.
                            if (dark)
                            {
                                rule.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PostItBorderBrush");
                            }
                            else
                            {
                                rule.ClearValue(System.Windows.Shapes.Shape.StrokeProperty);
                            }
                        }
                    }
                }
                else if (block is Section section)
                {
                    var sectionTextBrushKey = textBrushKey;
                    if (quoteStyle != null && ReferenceEquals(section.Style, quoteStyle))
                    {
                        sectionTextBrushKey = "PreviewQuoteTextBrush";
                        SetOrClearReference(section, Block.BorderBrushProperty, "PreviewQuoteBorderBrush", dark);
                        // Also the section itself, so list bullets inside a quote dim to match.
                        SetOrClearReference(section, TextElement.ForegroundProperty, sectionTextBrushKey, dark);
                    }
                    ApplyPreviewTheme(section.Blocks, dark, sectionTextBrushKey);
                }
                else if (block is List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        ApplyPreviewTheme(item.Blocks, dark, textBrushKey);
                    }
                }
                else if (block is Table table)
                {
                    SetOrClearReference(table, Block.BorderBrushProperty, "PreviewTableBorderBrush", dark);
                    foreach (var row in table.RowGroups.SelectMany(g => g.Rows))
                    {
                        if (tableHeaderStyle != null && ReferenceEquals(row.Style, tableHeaderStyle))
                        {
                            SetOrClearReference(row, TextElement.BackgroundProperty, "PostItToolbarBrush", dark);
                        }
                        foreach (var cell in row.Cells)
                        {
                            SetOrClearReference(cell, TableCell.BorderBrushProperty, "PreviewTableBorderBrush", dark);
                            ApplyPreviewTheme(cell.Blocks, dark, textBrushKey);
                        }
                    }
                }
            }
        }

        private static void SetOrClearReference(FrameworkContentElement element, DependencyProperty property, string resourceKey, bool set)
        {
            if (set)
            {
                element.SetResourceReference(property, resourceKey);
            }
            else
            {
                element.ClearValue(property);
            }
        }

        // Every inline at any depth — links and code spans can sit inside Bold/Italic/Hyperlink
        // Spans, the same nesting FindRuns handles for fonts.
        private static IEnumerable<Inline> FindInlines(IEnumerable<Inline> inlines)
        {
            foreach (var inline in inlines)
            {
                yield return inline;
                if (inline is Span span)
                {
                    foreach (var nested in FindInlines(span.Inlines))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static void SetOrClearFontFamily(TextElement element, string fontFamily)
        {
            if (fontFamily == null)
            {
                element.ClearValue(TextElement.FontFamilyProperty);
            }
            else
            {
                element.FontFamily = new FontFamily(fontFamily);
            }
        }

        // An inline code span inside emphasis (**`code`**) or a link ([`code`](url)) is a Run
        // nested one level inside a Bold/Italic/Hyperlink — all of which are Spans — rather than
        // a direct child of the paragraph's Inlines. Without this recursion those spans silently
        // keep the default font while every top-level code span switches.
        private static IEnumerable<Run> FindRuns(IEnumerable<Inline> inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    yield return run;
                }
                else if (inline is Span span)
                {
                    foreach (var nested in FindRuns(span.Inlines))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private void ApplyEditorFontSize()
        {
            EditorTextBox.FontSize = _fontSizeSettings.EditorFontSize;
        }

        // Markdig.Wpf's default styles set absolute FontSize values on heading paragraphs
        // (h1-h4), which override property-value inheritance — so setting Document.FontSize
        // scales body text but leaves headings (and checkbox glyphs) frozen. A LayoutTransform
        // scales the whole rendered visual tree uniformly instead, and still reflows to the
        // window width since LayoutTransform (unlike RenderTransform) re-measures.
        private void ApplyPreviewFontSize()
        {
            var scale = _fontSizeSettings.PreviewFontSize / FontSizing.Default;
            PreviewViewer.LayoutTransform = new ScaleTransform(scale, scale);
        }

        // Markdig.Wpf renders task-list items as real (but disabled) CheckBox controls — this
        // enables them and wires each one, by its position among all checkboxes in document
        // order, to Scratchpad.Core.TaskListToggler so a click edits the underlying markdown.
        // Markdig.Wpf only rebuilds the document when the Markdown text changes, so toggling the
        // preview off and on without an edit re-wires the *same* checkboxes. Removing the handler
        // before adding it keeps exactly one per checkbox; with two, a single click toggled the
        // task and then immediately toggled it back. The index rides on Tag for the same reason —
        // a fresh closure per wiring would make each subscription a distinct, unremovable handler.
        private void WireTaskListCheckBoxes()
        {
            var index = 0;
            foreach (var checkBox in FindTaskListCheckBoxes(PreviewViewer.Document.Blocks))
            {
                checkBox.IsEnabled = true;
                checkBox.Tag = index;
                checkBox.Click -= TaskListCheckBox_Click;
                checkBox.Click += TaskListCheckBox_Click;
                index++;
            }
        }

        private void TaskListCheckBox_Click(object sender, RoutedEventArgs e)
        {
            OnTaskListCheckBoxClicked((int)((CheckBox)sender).Tag);
        }

        private void OnTaskListCheckBoxClicked(int taskIndex)
        {
            EditorTextBox.Text = TaskListToggler.Toggle(EditorTextBox.Text, taskIndex);
            RenderPreview();
        }

        private static IEnumerable<CheckBox> FindTaskListCheckBoxes(IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is InlineUIContainer container && container.Child is CheckBox checkBox)
                        {
                            yield return checkBox;
                        }
                    }
                }
                else if (block is Section section)
                {
                    foreach (var checkBox in FindTaskListCheckBoxes(section.Blocks))
                    {
                        yield return checkBox;
                    }
                }
                else if (block is List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        foreach (var checkBox in FindTaskListCheckBoxes(item.Blocks))
                        {
                            yield return checkBox;
                        }
                    }
                }
            }
        }

        private void DateButton_Click(object sender, RoutedEventArgs e)
        {
            InsertDate();
        }

        // Types today's date at the cursor, replacing any selection as typing would. Assigning
        // SelectedText (rather than rebuilding Text) keeps it one undoable edit and leaves the
        // scroll position alone. From the preview, switches to the editor first, place kept.
        private void InsertDate()
        {
            if (PreviewScrollViewer.IsVisible)
            {
                SwitchToEditorAt(null);
                // A selection made before switching to the preview can survive the switch (when
                // the place can't be kept); replacing text the user can't see would be a surprise.
                EditorTextBox.Select(EditorTextBox.CaretIndex, 0);
            }

            var stamp = DateStamp.Format(_dateFormatSettings.Format, DateTime.Now, CultureInfo.CurrentCulture);
            EditorTextBox.SelectedText = stamp;
            EditorTextBox.CaretIndex = EditorTextBox.SelectionStart + stamp.Length;
            EditorTextBox.Focus();
        }

        // Replaces just the touched lines via SelectedText, which leaves the scroll position alone,
        // then selects what LineIndenter says to. The change block opens while the user's own
        // selection is still in place: undo restores the selection captured when the block began,
        // so without it Ctrl+Z would leave the whole touched lines selected, one keystroke away
        // from being typed over.
        private void ApplyIndentEdit(IndentEdit edit)
        {
            EditorTextBox.BeginChange();
            try
            {
                EditorTextBox.Select(edit.ReplaceStart, edit.ReplaceLength);
                EditorTextBox.SelectedText = edit.Replacement;
                EditorTextBox.Select(edit.SelectionStart, edit.SelectionLength);
            }
            finally
            {
                EditorTextBox.EndChange();
            }
        }

        private void AlwaysOnTopToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = AlwaysOnTopToggleButton.IsChecked == true;
        }

        private void PeekButton_Click(object sender, RoutedEventArgs e) => StartPeek();

        // Tunnelling, so it sees the click before any control inside the note does: a
        // Ctrl+Shift+click on a checkbox, a button or the editor only peeks.
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                StartPeek();
                e.Handled = true;
            }
        }

        // A peek slides the note off the bottom of the desktop rather than hiding it, so its
        // taskbar button stays. Only Top moves; _peekHomeTop is where it goes back to.
        private void StartPeek()
        {
            switch (_peekState)
            {
                case PeekState.Idle:
                    _peekHomeTop = Top;
                    break;
                case PeekState.SlidingBack:
                    // Re-peeked on the way back: leave from here, but still return home.
                    break;
                default:
                    // Already leaving or away: nothing on-screen to click, but stay safe anyway.
                    return;
            }

            BeginSlide(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight, PeekState.SlidingOut);
        }

        private void OnPeekTimerTick(object sender, EventArgs e)
        {
            _peekTimer.Stop();
            if (_isExiting || _peekState != PeekState.Away)
            {
                return;
            }
            // Raise while still off-screen, so the slide back plays in front of a window the
            // user clicked meanwhile rather than appearing only at the end.
            RaiseWithoutActivating();
            BeginSlide(_peekHomeTop, PeekState.SlidingBack);
        }

        private void BeginSlide(double to, PeekState state)
        {
            _peekState = state;
            _slideFrom = Top;
            _slideTo = to;

            if (PeekSlideDuration <= TimeSpan.Zero)
            {
                DetachSlideFrames();
                Top = to;
                FinishSlide();
                return;
            }

            _slideClock.Restart();
            if (!_isSlideFrameAttached)
            {
                CompositionTarget.Rendering += OnSlideFrame;
                _isSlideFrameAttached = true;
            }
        }

        // Once per WPF frame, so the slide keeps pace with the display rather than a timer.
        private void OnSlideFrame(object sender, EventArgs e)
        {
            var progress = _slideClock.Elapsed.TotalMilliseconds / PeekSlideDuration.TotalMilliseconds;
            Top = PeekSlide.Interpolate(_slideFrom, _slideTo, progress, accelerate: _peekState == PeekState.SlidingOut);
            if (progress >= 1)
            {
                DetachSlideFrames();
                FinishSlide();
            }
        }

        private void FinishSlide()
        {
            if (_peekState == PeekState.SlidingOut)
            {
                _peekState = PeekState.Away;
                _peekTimer.Interval = TimeSpan.FromSeconds(_peekSettings.Seconds);
                _peekTimer.Start();
            }
            else if (_peekState == PeekState.SlidingBack)
            {
                Top = _peekHomeTop;
                _peekState = PeekState.Idle;
                // Again here for the zero-duration jump, which never saw the raise in the tick.
                RaiseWithoutActivating();
            }
        }

        // If the user clicked a window underneath while the note was away, that window rose
        // above it. WPF's Topmost setter raises with SWP_NOACTIVATE, so this brings the note to
        // the front without taking focus or changing always-on-top.
        private void RaiseWithoutActivating()
        {
            if (!Topmost)
            {
                Topmost = true;
                Topmost = false;
            }
        }

        private void DetachSlideFrames()
        {
            if (_isSlideFrameAttached)
            {
                CompositionTarget.Rendering -= OnSlideFrame;
                _isSlideFrameAttached = false;
            }
        }

        // Puts the note straight back home. Everything that interrupts a peek (showing, closing,
        // exiting, a taskbar click) comes through here, so an off-screen Top is never saved.
        private void CancelPeek()
        {
            if (_peekState == PeekState.Idle)
            {
                return;
            }
            _peekTimer.Stop();
            DetachSlideFrames();
            Top = _peekHomeTop;
            _peekState = PeekState.Idle;
        }

        // A tray click: ends a peek early (with focus, as Show does); otherwise it does nothing.
        public void EndPeekEarly()
        {
            if (_peekState != PeekState.Idle)
            {
                ShowAndActivate();
            }
        }

        // Only the user can activate an off-screen note (taskbar button, Alt+Tab), so that ends
        // the peek. A peek normally starts with the note already active, which raises nothing.
        private void Window_Activated(object sender, EventArgs e)
        {
            if (_peekState == PeekState.SlidingOut || _peekState == PeekState.Away)
            {
                CancelPeek();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
            {
                SetPreviewMode(!(PreviewToggleButton.IsChecked == true));
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemSemicolon)
            {
                InsertDate();
                e.Handled = true;
            }
            else if (EditorTextBox.IsKeyboardFocused && e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
            {
                // Null only when the selection stays on one line: Tab is left to type a tab as usual.
                var edit = LineIndenter.Indent(EditorTextBox.Text, EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
                if (edit != null)
                {
                    ApplyIndentEdit(edit);
                    e.Handled = true;
                }
            }
            else if (EditorTextBox.IsKeyboardFocused && e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var edit = LineIndenter.Outdent(EditorTextBox.Text, EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
                if (edit != null)
                {
                    ApplyIndentEdit(edit);
                }
                // Handled even when there was nothing to remove: with AcceptsTab the TextBox would
                // otherwise type a tab, replacing any selection.
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
            {
                AlwaysOnTopToggleButton.IsChecked = !(AlwaysOnTopToggleButton.IsChecked == true);
                Topmost = AlwaysOnTopToggleButton.IsChecked == true;
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ~ModifierKeys.Shift) == ModifierKeys.Control
                && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                // '+' is Shift+OemPlus on standard keyboards, so "Ctrl and the + key" is
                // physically Ctrl+Shift+OemPlus — Shift is allowed here (unlike the other
                // branches) so that gesture, and plain Ctrl+OemPlus (the '=' key), both work.
                AdjustFontSize(1);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                AdjustFontSize(-1);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                ResetFontSize();
                e.Handled = true;
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AdjustFontSize(Math.Sign(e.Delta));
                e.Handled = true;
            }
        }

        // Markdig.Wpf's FlowDocumentScrollViewer has its own inner ScrollViewer. Given unlimited
        // height by PreviewScrollViewer it never scrolls, but WPF ScrollViewers mark every wheel
        // event handled regardless, so the wheel never reached the outer viewer that actually
        // scrolls. Catching it here, while it tunnels down, scrolls the outer one directly.
        // Ctrl+wheel never gets here: Window_PreviewMouseWheel tunnels first and handles it.
        private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Same distance per notch as a ScrollViewer's own wheel handling (WheelScrollLines
            // lines of 16px), scaled by Delta so high-resolution wheels and touchpads scroll
            // smoothly.
            var distance = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine * SystemParameters.WheelScrollLines * 16;
            PreviewScrollViewer.ScrollToVerticalOffset(PreviewScrollViewer.VerticalOffset - distance);
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Publish any edit still waiting out its debounce delay — otherwise up to 500ms of
            // typing is lost if the user exits right after typing. Safe to call even if nothing
            // was ever typed (e.g. the window was never shown): Flush() is a no-op with no
            // pending change.
            _autosave.Flush();

            // Back home before the placement below is read, so a peek never saves off-screen.
            CancelPeek();

            // Left/Top/Width/Height are still Double.NaN if this window was never shown (e.g.
            // the app started minimized to the tray and was exited straight from the tray menu)
            // — Window_Loaded is what assigns them real values. Saving NaN would otherwise throw
            // out of WindowPlacementStore and crash the app on its most common exit path.
            if (!double.IsNaN(Left) && !double.IsNaN(Top) && !double.IsNaN(Width) && !double.IsNaN(Height))
            {
                // Persist the restored (non-maximized/minimized) bounds, not whatever the OS
                // reports for Left/Top/Width/Height while maximized or minimized — otherwise
                // reopening after exiting maximized fills the screen non-maximized, and exiting
                // minimized can persist an off-screen position like (-32000, -32000).
                var bounds = WindowState == WindowState.Normal
                    ? new WindowBounds { Left = Left, Top = Top, Width = Width, Height = Height }
                    : new WindowBounds
                    {
                        Left = RestoreBounds.Left,
                        Top = RestoreBounds.Top,
                        Width = RestoreBounds.Width,
                        Height = RestoreBounds.Height
                    };
                _placementStore.Save(bounds);
            }

            // No NaN-style guard needed here, unlike window placement: _fontSizeSettings only
            // ever holds values produced by FontSizing's clamped operations or its own Default.
            _fontSizeStore.Save(_fontSizeSettings);

            if (!_isExiting)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void ShowAndActivate()
        {
            // Any other way of showing the note ends a peek, putting it straight back home.
            CancelPeek();
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();

            // When this is invoked from a second launch's notification, the foreground right
            // belongs to that (about-to-exit) process, not this one, so Activate() alone can be
            // downgraded by Windows to a flashing taskbar button. Briefly toggling Topmost pulls
            // the window to the front regardless — but only when it isn't already pinned
            // on top, so this never clobbers the user's own Always-on-Top choice.
            if (!Topmost)
            {
                Topmost = true;
                Topmost = false;
            }
        }

        public void MarkExiting()
        {
            _isExiting = true;
            CancelPeek();
            _globalHotkey.Dispose();
        }

        // Called by App once the tray icon exists, so a key another program already owns can be
        // reported there — otherwise the hotkey would just silently not work.
        public void StartGlobalHotkey(Action<string> showWarning)
        {
            try
            {
                if (!_globalHotkey.Start(_hotkeyStore.Load()))
                {
                    showWarning("Couldn't use " + Hotkey.Format(_globalHotkey.Current)
                        + " as the hotkey because another program has it. Choose another key in Settings.");
                }
            }
            catch
            {
                // The hotkey is a convenience; the note is still reachable from the tray.
            }
        }

        // The hotkey only brings the note forward, ready to type; the user leaves with Alt+Tab.
        // From the preview it switches to the editor as Ctrl+E does (caret at the top block).
        private void OnGlobalHotkeyPressed()
        {
            ShowAndActivate();
            if (PreviewToggleButton.IsChecked == true)
            {
                SwitchToEditorAt(null);
            }
            // Deferred: on the first show after a hidden start, Window_Loaded runs inside
            // ShowAndActivate and focus set now can be lost before the window settles.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => EditorTextBox.Focus()));
        }
    }
}
