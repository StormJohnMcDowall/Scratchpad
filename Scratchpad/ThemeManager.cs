using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Scratchpad.Core;

namespace Scratchpad
{
    // Owns which palette is merged into Application.Resources. Everything themed references its
    // brushes with DynamicResource (or SetResourceReference), so swapping the dictionary here
    // re-colours every open — or hidden — window without any of them being told to.
    public sealed class ThemeManager : IDisposable
    {
        private static readonly Uri LightUri = new Uri("pack://application:,,,/Scratchpad;component/Themes/Light.xaml");
        private static readonly Uri DarkUri = new Uri("pack://application:,,,/Scratchpad;component/Themes/Dark.xaml");

        private readonly ThemeStore _store;
        private readonly IPersonalizeRegistry _registry;
        private bool _subscribed;

        public ThemePreference Preference { get; private set; } = ThemeStore.Defaults.Preference;
        public AppTheme Current { get; private set; } = AppTheme.Light;

        public event Action<AppTheme> ThemeChanged;

        public ThemeManager(ThemeStore store, IPersonalizeRegistry registry)
        {
            _store = store;
            _registry = registry;
        }

        public void Start()
        {
            Preference = _store.Load().Preference;
            Apply();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _subscribed = true;
        }

        public void SetPreference(ThemePreference preference)
        {
            Preference = preference;
            _store.Save(new ThemeSettings { Preference = preference });
            Apply();
        }

        public void Dispose()
        {
            // SystemEvents is static: a handler left attached outlives the app's windows.
            if (_subscribed)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _subscribed = false;
            }
        }

        // Raised for many unrelated settings too; re-resolving is one registry read, so no need
        // to filter on category (which Windows doesn't report consistently for app-mode changes).
        // May arrive on SystemEvents' own thread, so hop to the UI thread before touching WPF.
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
        }

        private void Apply()
        {
            var theme = ThemeResolver.Resolve(Preference, ThemeResolver.WindowsAppsUseLightTheme(_registry));
            var merged = Application.Current.Resources.MergedDictionaries;
            var existing = merged.FirstOrDefault(d => d.Source == LightUri || d.Source == DarkUri
                || (d.Source != null && d.Source.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase)));
            var wanted = theme == AppTheme.Dark ? DarkUri : LightUri;

            if (existing == null || existing.Source != wanted)
            {
                if (existing != null)
                {
                    merged.Remove(existing);
                }
                merged.Add(new ResourceDictionary { Source = wanted });
            }

            if (theme != Current)
            {
                Current = theme;
                ThemeChanged?.Invoke(theme);
            }
        }
    }
}
