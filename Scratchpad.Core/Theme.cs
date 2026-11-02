using Microsoft.Win32;

namespace Scratchpad.Core
{
    // Persisted by number in theme.json, so the values are pinned explicitly.
    public enum ThemePreference
    {
        FollowWindows = 0,
        Light = 1,
        Dark = 2
    }

    public enum AppTheme
    {
        Light,
        Dark
    }

    public interface IPersonalizeRegistry
    {
        object GetAppsUseLightTheme();
    }

    public class RegistryPersonalizeKey : IPersonalizeRegistry
    {
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

        public object GetAppsUseLightTheme()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, false))
            {
                return key?.GetValue(AppsUseLightThemeValueName);
            }
        }
    }

    public static class ThemeResolver
    {
        // Windows' "default app mode" (Personalisation → Colours), not the separate taskbar/Start
        // mode. Anything other than an explicit DWORD 0 — missing value (older Windows), a locked
        // key, an unexpected type — means light, which is what Scratchpad always looked like.
        public static bool WindowsAppsUseLightTheme(IPersonalizeRegistry registry)
        {
            try
            {
                return !(registry.GetAppsUseLightTheme() is int value && value == 0);
            }
            catch
            {
                return true;
            }
        }

        public static AppTheme Resolve(ThemePreference preference, bool windowsAppsUseLightTheme)
        {
            switch (preference)
            {
                case ThemePreference.Light:
                    return AppTheme.Light;
                case ThemePreference.Dark:
                    return AppTheme.Dark;
                default:
                    return windowsAppsUseLightTheme ? AppTheme.Light : AppTheme.Dark;
            }
        }
    }
}
