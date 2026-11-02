using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Scratchpad.Core;

namespace Scratchpad
{
    public partial class SettingsWindow : Window
    {
        // The preview pickers can be "unset" (null = follow Markdig.Wpf's own default), but a
        // ComboBox with no selected item looks broken and gives no way back to that state once a
        // real font is chosen. This sentinel is a real FontFamily instance (so it plugs into the
        // existing ItemsSource/DisplayMemberPath="Source" plumbing unchanged) that both combos
        // recognize by reference, never by its Source string, so it can never collide with an
        // actual installed font family of the same name.
        private static readonly FontFamily DefaultSentinel = new FontFamily("(default)");

        private readonly IReadOnlyList<FontFamily> _monospaceFamilies;
        private readonly IReadOnlyList<FontFamily> _normalTextFamilies;
        private bool _isUpdatingSelection;
        private AppTheme _theme;

        public event Action<FontFamilySettings> SettingsChanged;
        public event Action<ThemePreference> ThemePreferenceChanged;
        public event Action<bool> ShowWindowOnStartupChanged;
        public event Action ResetRequested;

        public SettingsWindow(FontFamilySettings current, ThemePreference currentThemePreference, AppTheme currentTheme,
            bool showWindowOnStartup, IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)
        {
            InitializeComponent();
            _theme = currentTheme;
            // The title bar is drawn by Windows and needs the HWND, which only exists from here.
            SourceInitialized += (s, e) => ApplyTheme(_theme);

            var codeFontChoices = new[] { DefaultSentinel }.Concat(monospaceFamilies).ToList();
            var normalTextChoices = new[] { DefaultSentinel }.Concat(normalTextFamilies).ToList();

            EditorFontComboBox.ItemsSource = monospaceFamilies;
            PreviewCodeFontComboBox.ItemsSource = codeFontChoices;
            PreviewNormalFontComboBox.ItemsSource = normalTextChoices;

            _monospaceFamilies = monospaceFamilies;
            _normalTextFamilies = normalTextFamilies;
            ShowSettings(current, currentThemePreference, showWindowOnStartup);

            ThemeComboBox.SelectionChanged += (s, e) => RaiseThemePreferenceChanged();
            ShowOnStartupCheckBox.Checked += (s, e) => RaiseShowWindowOnStartupChanged();
            ShowOnStartupCheckBox.Unchecked += (s, e) => RaiseShowWindowOnStartupChanged();
            EditorFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewNormalFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewCodeFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
        }

        // Re-selects the pickers to match settings changed from outside this window (a reset).
        // Each SelectedItem assignment fires SelectionChanged; without the guard every one would
        // push a half-updated value back out to be applied and saved.
        public void ShowSettings(FontFamilySettings settings, ThemePreference themePreference, bool showWindowOnStartup)
        {
            _isUpdatingSelection = true;
            try
            {
                ShowOnStartupCheckBox.IsChecked = showWindowOnStartup;
                ThemeComboBox.SelectedItem = ThemeComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => (ThemePreference)item.Tag == themePreference);
                EditorFontComboBox.SelectedItem = _monospaceFamilies.FirstOrDefault(f => f.Source == settings.EditorFontFamily);
                PreviewNormalFontComboBox.SelectedItem = settings.PreviewNormalFontFamily == null
                    ? DefaultSentinel
                    : _normalTextFamilies.FirstOrDefault(f => f.Source == settings.PreviewNormalFontFamily);
                PreviewCodeFontComboBox.SelectedItem = settings.PreviewCodeFontFamily == null
                    ? DefaultSentinel
                    : _monospaceFamilies.FirstOrDefault(f => f.Source == settings.PreviewCodeFontFamily);
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this, "Reset all settings to their defaults?", "Scratchpad",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ResetRequested?.Invoke();
            }
        }

        // Dark: Post-it colours by resource reference (so a later palette swap follows) plus a dark
        // native title bar. Light: clear back to the plain system look this window always had.
        // Its controls (labels, group boxes, combo boxes, button) are themed by Dark.xaml's
        // implicit styles, which simply don't exist while the light palette is merged.
        public void ApplyTheme(AppTheme theme)
        {
            _theme = theme;
            if (theme == AppTheme.Dark)
            {
                SetResourceReference(BackgroundProperty, "PostItBackgroundBrush");
                SetResourceReference(ForegroundProperty, "PostItTextBrush");
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(ForegroundProperty);
            }
            SetDarkTitleBar(theme == AppTheme.Dark);
        }

        private const int DwmwaUseImmersiveDarkMode = 20;
        // Same attribute under its pre-release number, used by Windows 10 builds before 20H1.
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        // Best-effort: older Windows builds reject both attributes, and the title bar simply
        // stays light. Never allowed to throw — it's cosmetic.
        private void SetDarkTitleBar(bool dark)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                var value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
                }

                // An already-visible window otherwise keeps its old title bar until it is next
                // deactivated; a frame-changed no-op repositioning makes Windows repaint it now.
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            }
            catch
            {
                // See comment above.
            }
        }

        private void RaiseShowWindowOnStartupChanged()
        {
            if (_isUpdatingSelection)
            {
                return;
            }

            ShowWindowOnStartupChanged?.Invoke(ShowOnStartupCheckBox.IsChecked == true);
        }

        private void RaiseThemePreferenceChanged()
        {
            if (_isUpdatingSelection || !(ThemeComboBox.SelectedItem is ComboBoxItem item))
            {
                return;
            }

            ThemePreferenceChanged?.Invoke((ThemePreference)item.Tag);
        }

        private void RaiseSettingsChanged()
        {
            if (_isUpdatingSelection)
            {
                return;
            }

            SettingsChanged?.Invoke(new FontFamilySettings
            {
                EditorFontFamily = (EditorFontComboBox.SelectedItem as FontFamily)?.Source,
                PreviewNormalFontFamily = ResolveSelection(PreviewNormalFontComboBox),
                PreviewCodeFontFamily = ResolveSelection(PreviewCodeFontComboBox)
            });
        }

        private static string ResolveSelection(ComboBox comboBox)
        {
            return ReferenceEquals(comboBox.SelectedItem, DefaultSentinel)
                ? null
                : (comboBox.SelectedItem as FontFamily)?.Source;
        }
    }
}
