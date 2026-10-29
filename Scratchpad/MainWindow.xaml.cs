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
        // Initialized to real defaults (not the struct's zero-value) so that if the window is
        // closed without ever being shown — e.g. exited from the tray before Window_Loaded ever
        // runs — Closing still persists usable sizes instead of 0pt fonts. Same bug class as the
        // NaN guard on window placement.
        private FontSizeSettings _fontSizeSettings = new FontSizeSettings
        {
            EditorFontSize = FontSizing.Default,
            PreviewFontSize = FontSizing.Default
        };
        private readonly DebouncedAutosaveCoordinator _autosave;
        private bool _isExiting;

        public MainWindow()
        {
            InitializeComponent();
            _autosave = new DebouncedAutosaveCoordinator(_documentStore.Save, TimeSpan.FromMilliseconds(500));
            // Must match Scratchpad.Core.TaskListToggler's pipeline so checkbox positions agree
            // between what's rendered here and what gets toggled there.
            PreviewViewer.Pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Text = _documentStore.Load();

            _fontSizeSettings = _fontSizeStore.Load();
            EditorTextBox.FontSize = _fontSizeSettings.EditorFontSize;

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
            PreviewViewer.Document.FontSize = _fontSizeSettings.PreviewFontSize;
            WireTaskListCheckBoxes();
        }

        private void AdjustFontSize(int direction)
        {
            if (PreviewToggleButton.IsChecked == true)
            {
                _fontSizeSettings.PreviewFontSize = direction > 0
                    ? FontSizing.Increase(_fontSizeSettings.PreviewFontSize)
                    : FontSizing.Decrease(_fontSizeSettings.PreviewFontSize);
                PreviewViewer.Document.FontSize = _fontSizeSettings.PreviewFontSize;
            }
            else
            {
                _fontSizeSettings.EditorFontSize = direction > 0
                    ? FontSizing.Increase(_fontSizeSettings.EditorFontSize)
                    : FontSizing.Decrease(_fontSizeSettings.EditorFontSize);
                EditorTextBox.FontSize = _fontSizeSettings.EditorFontSize;
            }
        }

        private void ResetFontSize()
        {
            if (PreviewToggleButton.IsChecked == true)
            {
                _fontSizeSettings.PreviewFontSize = FontSizing.Default;
                PreviewViewer.Document.FontSize = FontSizing.Default;
            }
            else
            {
                _fontSizeSettings.EditorFontSize = FontSizing.Default;
                EditorTextBox.FontSize = FontSizing.Default;
            }
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-1);
        private void ZoomInButton_Click(object sender, RoutedEventArgs e) => AdjustFontSize(1);

        // Markdig.Wpf renders task-list items as real (but disabled) CheckBox controls — this
        // enables them and wires each one, by its position among all checkboxes in document
        // order, to Scratchpad.Core.TaskListToggler so a click edits the underlying markdown.
        private void WireTaskListCheckBoxes()
        {
            var index = 0;
            foreach (var checkBox in FindTaskListCheckBoxes(PreviewViewer.Document.Blocks))
            {
                checkBox.IsEnabled = true;
                var taskIndex = index;
                checkBox.Click += (s, e) => OnTaskListCheckBoxClicked(taskIndex);
                index++;
            }
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
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
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
