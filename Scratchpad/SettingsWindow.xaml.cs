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
        public event Action<FontFamilySettings> SettingsChanged;

        public SettingsWindow(FontFamilySettings current, IReadOnlyList<FontFamily> monospaceFamilies, IReadOnlyList<FontFamily> normalTextFamilies)
        {
            InitializeComponent();

            EditorFontComboBox.ItemsSource = monospaceFamilies;
            PreviewCodeFontComboBox.ItemsSource = monospaceFamilies;
            PreviewNormalFontComboBox.ItemsSource = normalTextFamilies;

            EditorFontComboBox.SelectedItem = monospaceFamilies.FirstOrDefault(f => f.Source == current.EditorFontFamily);
            PreviewNormalFontComboBox.SelectedItem = normalTextFamilies.FirstOrDefault(f => f.Source == current.PreviewNormalFontFamily);
            PreviewCodeFontComboBox.SelectedItem = monospaceFamilies.FirstOrDefault(f => f.Source == current.PreviewCodeFontFamily);

            EditorFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewNormalFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
            PreviewCodeFontComboBox.SelectionChanged += (s, e) => RaiseSettingsChanged();
        }

        private void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke(new FontFamilySettings
            {
                EditorFontFamily = (EditorFontComboBox.SelectedItem as FontFamily)?.Source,
                PreviewNormalFontFamily = (PreviewNormalFontComboBox.SelectedItem as FontFamily)?.Source,
                PreviewCodeFontFamily = (PreviewCodeFontComboBox.SelectedItem as FontFamily)?.Source
            });
        }
    }
}
