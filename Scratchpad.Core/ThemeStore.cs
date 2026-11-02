using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class ThemeStore
    {
        // What a fresh install gets; also what Settings' "Reset to defaults" restores.
        public static ThemeSettings Defaults { get; } = new ThemeSettings
        {
            Preference = ThemePreference.FollowWindows
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "theme.json");

        public string FilePath { get; }

        public ThemeStore(string filePath)
        {
            FilePath = filePath;
        }

        public ThemeSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<ThemeSettings>(json);
                // An enum deserializes any number, so a hand-edited or future-version value
                // outside the known set falls back rather than reaching the resolver.
                return Enum.IsDefined(typeof(ThemePreference), loaded.Preference) ? loaded : Defaults;
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(ThemeSettings settings)
        {
            // Theme, like the font settings, is a nicety rather than the app's core promise — a
            // failure here must never crash the app.
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempPath = FilePath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(settings));

                if (File.Exists(FilePath))
                {
                    File.Replace(tempPath, FilePath, null);
                }
                else
                {
                    File.Move(tempPath, FilePath);
                }
            }
            catch
            {
                // Best-effort; see comment above.
            }
        }
    }
}
