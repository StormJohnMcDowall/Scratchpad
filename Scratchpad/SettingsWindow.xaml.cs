using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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

        public event Action<FontFamilySettings> SettingsChanged;

        public SettingsWindow(FontFamilySettings current, IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)
        {
            InitializeComponent();

            var codeFontChoices = new[] { DefaultSentinel }.Concat(monospaceFamilies).ToList();
            var normalTextChoices = new[] { DefaultSentinel }.Concat(normalTextFamilies).ToList();

            EditorFontComboBox.ItemsSource = monospaceFamilies;
            PreviewCodeFontComboBox.ItemsSource = codeFontChoices;
            PreviewNormalFontComboBox.ItemsSource = normalTextChoices;

            EditorFontComboBox.SelectedItem = monospaceFamilies.FirstOrDefault(f => f.Source == current.EditorFontFamily);
            PreviewNormalFontComboBox.SelectedItem = current.PreviewNormalFontFamily == null
                ? DefaultSentinel
                : normalTextFamilies.FirstOrDefault(f => f.Source == current.PreviewNormalFontFamily);
            PreviewCodeFontComboBox.SelectedItem = current.PreviewCodeFontFamily == null
                ? DefaultSentinel
                : monospaceFamilies.FirstOrDefault(f => f.Source == current.PreviewCodeFontFamily);

            EditorFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewNormalFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewCodeFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
        }

        private void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke(new FontFamilySettings
            {
                EditorFontFamily = (EditorFontComboBox.SelectedItem as FontFamily)?.Source,
                PreviewNormalFontFamily = ResolveSelection(PreviewNormalFontComboBox),
                PreviewCodeFontFamily = ResolveSelection(PreviewCodeFontComboBox)
            });
        }

        private static string ResolveSelection(System.Windows.Controls.ComboBox comboBox)
        {
            return ReferenceEquals(comboBox.SelectedItem, DefaultSentinel)
                ? null
                : (comboBox.SelectedItem as FontFamily)?.Source;
        }
    }
}
