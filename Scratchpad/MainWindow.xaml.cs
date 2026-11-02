using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        private SettingsWindow _settingsWindow;
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
            PreviewToggleButton.IsChecked = showPreview;
            if (showPreview)
            {
                RenderPreview();
                EditorTextBox.Visibility = Visibility.Collapsed;
                PreviewScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                EditorTextBox.Visibility = Visibility.Visible;
                PreviewScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void RenderPreview()
        {
            PreviewViewer.Markdown = EditorTextBox.Text;
            ApplyPreviewFontSize();
            ApplyPreviewFonts();
            ApplyPreviewTheme();
            ApplyPreviewBackground();
            WireTaskListCheckBoxes();
        }

        // Markdig.Wpf creates a fresh FlowDocument whenever the Markdown text changes (the same reason
        // ApplyPreviewFontSize/ApplyPreviewFonts already exist as post-render steps) — the
        // ScrollViewer's own Background alone isn't visible if that FlowDocument paints its own
        // opaque background on top.
        private void ApplyPreviewBackground()
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            // A resource reference rather than a one-off lookup, so a theme switch while the
            // preview is showing re-colours it too.
            PreviewViewer.Document.SetResourceReference(FlowDocument.BackgroundProperty, "PostItBackgroundBrush");
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
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
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
                _startupWindowSettings.ShowWindowOnStartup, FontCatalog.MonospaceFamilies, FontCatalog.NormalTextFamilies);
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
        // theme and show-on-startup reset. Font sizes are saved here rather than waiting for the usual on-hide save.
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

            _settingsWindow?.ShowSettings(_fontFamilySettings, _themeManager.Preference, _startupWindowSettings.ShowWindowOnStartup);
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
            ApplyPreviewTheme(PreviewViewer.Document.Blocks, dark);
        }

        private void ApplyPreviewTheme(IEnumerable<Block> blocks, bool dark)
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
                    SetOrClearReference(paragraph, TextElement.ForegroundProperty, "PostItTextBrush", dark);

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
                            SetOrClearReference(inline, TextElement.ForegroundProperty, "PostItTextBrush", dark);
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
                    if (quoteStyle != null && ReferenceEquals(section.Style, quoteStyle))
                    {
                        SetOrClearReference(section, Block.BorderBrushProperty, "PreviewQuoteBorderBrush", dark);
                    }
                    ApplyPreviewTheme(section.Blocks, dark);
                }
                else if (block is List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        ApplyPreviewTheme(item.Blocks, dark);
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
                            ApplyPreviewTheme(cell.Blocks, dark);
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

        private void AlwaysOnTopToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = AlwaysOnTopToggleButton.IsChecked == true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
            {
                SetPreviewMode(!(PreviewToggleButton.IsChecked == true));
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Publish any edit still waiting out its debounce delay — otherwise up to 500ms of
            // typing is lost if the user exits right after typing. Safe to call even if nothing
            // was ever typed (e.g. the window was never shown): Flush() is a no-op with no
            // pending change.
            _autosave.Flush();

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
        }
    }
}
