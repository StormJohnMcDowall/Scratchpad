using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class HotkeyStore
    {
        // What a fresh install gets; also what Settings' "Reset to defaults" restores. Win+Shift+N:
        // apps almost never use the Win key, so a global grab doesn't steal an app's own shortcut.
        public static HotkeySettings Defaults { get; } = new HotkeySettings
        {
            Modifiers = HotkeyModifiers.Win | HotkeyModifiers.Shift,
            VirtualKey = 0x4E
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "hotkey.json");

        public string FilePath { get; }

        public HotkeyStore(string filePath)
        {
            FilePath = filePath;
        }

        public HotkeySettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<HotkeySettings>(json);
                if (settings.IsNone)
                {
                    // Turned off on purpose: stays off.
                    return HotkeySettings.None;
                }
                // A hand-edited file could hold Shift+A, which would break typing everywhere.
                return Hotkey.IsValid(settings) ? settings : Defaults;
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(HotkeySettings settings)
        {
            // Like the other settings, a nicety rather than the app's core promise — a failure
            // here must never crash the app.
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
