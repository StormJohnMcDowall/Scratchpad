using System;
using System.Windows;
using System.Windows.Input;
using Scratchpad.Core;

namespace Scratchpad
{
    public partial class MainWindow : Window
    {
        private readonly DocumentStore _documentStore = new DocumentStore(DocumentStore.DefaultFilePath);
        private readonly WindowPlacementStore _placementStore = new WindowPlacementStore(WindowPlacementStore.DefaultFilePath);
        private readonly DebouncedAutosaveCoordinator _autosave;
        private bool _isExiting;

        public MainWindow()
        {
            InitializeComponent();
            _autosave = new DebouncedAutosaveCoordinator(_documentStore.Save, TimeSpan.FromMilliseconds(500));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Text = _documentStore.Load();

            var requested = _placementStore.Load();
            var screenBounds = new WindowBounds
            {
                Left = SystemParameters.VirtualScreenLeft,
                Top = SystemParameters.VirtualScreenTop,
                Width = SystemParameters.VirtualScreenWidth,
                Height = SystemParameters.VirtualScreenHeight
            };
            var clamped = WindowPlacement.Clamp(requested, screenBounds);
            Left = clamped.Left;
            Top = clamped.Top;
            Width = clamped.Width;
            Height = clamped.Height;
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
                PreviewViewer.Markdown = EditorTextBox.Text;
                EditorTextBox.Visibility = Visibility.Collapsed;
                PreviewScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                EditorTextBox.Visibility = Visibility.Visible;
                PreviewScrollViewer.Visibility = Visibility.Collapsed;
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
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _placementStore.Save(new WindowBounds { Left = Left, Top = Top, Width = Width, Height = Height });

            if (!_isExiting)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void ShowAndActivate()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        public void MarkExiting()
        {
            _isExiting = true;
        }
    }
}
