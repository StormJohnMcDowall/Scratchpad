using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private int _peekSeconds;
        private HotkeySettings _hotkey;
        private bool _hotkeyWorking;
        private bool _isRecordingHotkey;

        public event Action<FontFamilySettings> SettingsChanged;
        public event Action<ThemePreference> ThemePreferenceChanged;
        public event Action<bool> ShowWindowOnStartupChanged;
        public event Action<string> DateFormatChanged;
        public event Action<int> PeekSecondsChanged;
        // Recording suspends the live hotkey (so pressing it reaches the box); ended is always
        // raised before chosen, so the old key is back in place before the swap is attempted.
        public event Action HotkeyCaptureStarted;
        public event Action HotkeyCaptureEnded;
        public event Action<HotkeySettings> HotkeyChosen;
        public event Action ResetRequested;

        public SettingsWindow(FontFamilySettings current, ThemePreference currentThemePreference, AppTheme currentTheme,
            bool showWindowOnStartup, string dateFormat, int peekSeconds, HotkeySettings hotkey, bool hotkeyWorking,
            IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)
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
            ShowSettings(current, currentThemePreference, showWindowOnStartup, dateFormat, peekSeconds);
            ShowHotkey(hotkey, hotkeyWorking, null);

            ThemeComboBox.SelectionChanged += (s, e) => RaiseThemePreferenceChanged();
            ShowOnStartupCheckBox.Checked += (s, e) => RaiseShowWindowOnStartupChanged();
            ShowOnStartupCheckBox.Unchecked += (s, e) => RaiseShowWindowOnStartupChanged();
            DateFormatTextBox.TextChanged += (s, e) => OnDateFormatTextChanged();
            PeekSecondsTextBox.TextChanged += (s, e) => OnPeekSecondsTextChanged();
            PeekSecondsTextBox.LostFocus += (s, e) => ShowPeekSeconds();
            EditorFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewNormalFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewCodeFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            HotkeyTextBox.GotKeyboardFocus += (s, e) => StartRecordingHotkey();
            // A click on the box when it already has focus (e.g. after recording one key) records again.
            HotkeyTextBox.PreviewMouseLeftButtonDown += (s, e) => StartRecordingHotkey();
            HotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
            HotkeyTextBox.LostKeyboardFocus += (s, e) => StopRecordingHotkey();
            // Closing mid-recording must still hand the suspended hotkey back.
            Closed += (s, e) => StopRecordingHotkey();
        }

        /// <summary>
        /// Shows the hotkey in use and a line about it. refused is the key Windows just turned
        /// down (only right after a failed change); working is false when the current key isn't
        /// registered, e.g. another program had it at startup.
        /// </summary>
        public void ShowHotkey(HotkeySettings current, bool working, HotkeySettings? refused)
        {
            _hotkey = current;
            _hotkeyWorking = working;
            if (!_isRecordingHotkey)
            {
                HotkeyTextBox.Text = Hotkey.Format(current);
            }

            if (refused.HasValue)
            {
                ShowHotkeyMessage(Hotkey.Format(refused.Value) + " is in use by another program. "
                    + (current.IsNone ? "Still no hotkey." : "Still using " + Hotkey.Format(current) + "."), true);
            }
            else if (!working && !current.IsNone)
            {
                ShowHotkeyMessage(Hotkey.Format(current) + " is in use by another program, so it isn't working. Choose another key.", true);
            }
            else
            {
                ShowHotkeyMessage(current.IsNone ? "No hotkey." : "Brings the note forward from any app.", false);
            }
        }

        // Same colouring as the Date example: a resource reference so a theme switch follows.
        private void ShowHotkeyMessage(string text, bool isWarning)
        {
            HotkeyMessageTextBlock.Text = text;
            if (isWarning)
            {
                HotkeyMessageTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "SettingsWarningBrush");
            }
            else
            {
                HotkeyMessageTextBlock.ClearValue(TextBlock.ForegroundProperty);
            }
        }

        private void StartRecordingHotkey()
        {
            if (_isRecordingHotkey)
            {
                return;
            }

            _isRecordingHotkey = true;
            HotkeyCaptureStarted?.Invoke();
            HotkeyTextBox.Text = "Press a key combination…";
            ShowHotkeyMessage("", false);
        }

        // Safe to call when not recording: leaving the box, closing and choosing all end up here.
        private void StopRecordingHotkey()
        {
            if (!_isRecordingHotkey)
            {
                return;
            }

            _isRecordingHotkey = false;
            ShowHotkey(_hotkey, _hotkeyWorking, null);
            HotkeyCaptureEnded?.Invoke();
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecordingHotkey)
            {
                return;
            }

            // With Alt held WPF reports Key.System and the real key separately; IME and dead-key
            // processing hide it the same way.
            var key = e.Key == Key.System ? e.SystemKey
                : e.Key == Key.ImeProcessed ? e.ImeProcessedKey
                : e.Key == Key.DeadCharProcessed ? e.DeadCharProcessedKey
                : e.Key;
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            // Not Keyboard.Modifiers: it never includes the Win key, so Win+Shift+N read as Shift+N.
            var modifiers = Hotkey.HeldModifiers(vk => Keyboard.IsKeyDown(KeyInterop.KeyFromVirtualKey(vk)));

            switch (Hotkey.Classify(modifiers, virtualKey))
            {
                case HotkeyKeyAction.KeepListening:
                    e.Handled = true;
                    break;
                case HotkeyKeyAction.Cancel:
                    e.Handled = true;
                    StopRecordingHotkey();
                    break;
                case HotkeyKeyAction.MoveFocus:
                    // Left for WPF: Tab moves on, and LostKeyboardFocus stops recording.
                    break;
                case HotkeyKeyAction.NeedsModifier:
                    e.Handled = true;
                    ShowHotkeyMessage("Include Win, Ctrl or Alt.", true);
                    break;
                default:
                    e.Handled = true;
                    var chosen = new HotkeySettings { Modifiers = modifiers, VirtualKey = virtualKey };
                    // A key WPF has no virtual-key code for comes back as 0, which would read
                    // as None and silently turn the hotkey off.
                    if (!Hotkey.IsValid(chosen))
                    {
                        break;
                    }
                    StopRecordingHotkey();
                    HotkeyChosen?.Invoke(chosen);
                    break;
            }
        }

        private void HotkeyNoneButton_Click(object sender, RoutedEventArgs e)
        {
            StopRecordingHotkey();
            HotkeyChosen?.Invoke(HotkeySettings.None);
        }

        // Re-selects the pickers to match settings changed from outside this window (a reset).
        // Each SelectedItem assignment fires SelectionChanged; without the guard every one would
        // push a half-updated value back out to be applied and saved.
        public void ShowSettings(FontFamilySettings settings, ThemePreference themePreference, bool showWindowOnStartup,
            string dateFormat, int peekSeconds)
        {
            _isUpdatingSelection = true;
            try
            {
                DateFormatTextBox.Text = dateFormat;
                _peekSeconds = peekSeconds;
                ShowPeekSeconds();
                UpdateDateExample();
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

        // Saved as typed, so a half-typed format is saved too; DateStamp falls back to the default
        // for anything .NET rejects, and the example shows exactly what Date would insert.
        private void OnDateFormatTextChanged()
        {
            UpdateDateExample();
            if (_isUpdatingSelection)
            {
                return;
            }

            DateFormatChanged?.Invoke(DateFormatTextBox.Text);
        }

        // Applied as typed, but the box is only rewritten on leaving it: rewriting mid-typing
        // would turn the "0" of a planned "0" → "10" edit into "1" under the user's cursor.
        private void OnPeekSecondsTextChanged()
        {
            if (_isUpdatingSelection)
            {
                return;
            }

            var seconds = PeekStore.ParseSeconds(PeekSecondsTextBox.Text);
            if (seconds.HasValue && seconds.Value != _peekSeconds)
            {
                _peekSeconds = seconds.Value;
                PeekSecondsChanged?.Invoke(_peekSeconds);
            }
        }

        // Shows the value actually in use: "0" becomes "1", and "abc" goes back to the last number.
        private void ShowPeekSeconds()
        {
            var wasUpdating = _isUpdatingSelection;
            _isUpdatingSelection = true;
            try
            {
                PeekSecondsTextBox.Text = _peekSeconds.ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                _isUpdatingSelection = wasUpdating;
            }
        }

        // Says why the example differs from what was typed when the default stands in. The
        // warning colour is a resource reference so a theme switch re-colours it; clearing it
        // (rather than setting black) hands the text back to the window's own Foreground.
        private void UpdateDateExample()
        {
            var stamp = DateStamp.Format(DateFormatTextBox.Text, DateTime.Now, CultureInfo.CurrentCulture, out var status);
            switch (status)
            {
                case DateFormatStatus.Blank:
                    DateExampleTextBlock.Text = "Using the default: " + stamp;
                    break;
                case DateFormatStatus.Rejected:
                    DateExampleTextBlock.Text = "Not a valid format, so Date uses: " + stamp;
                    break;
                default:
                    DateExampleTextBlock.Text = "Today: " + stamp;
                    break;
            }

            if (status == DateFormatStatus.Rejected)
            {
                DateExampleTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "SettingsWarningBrush");
            }
            else
            {
                DateExampleTextBlock.ClearValue(TextBlock.ForegroundProperty);
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
