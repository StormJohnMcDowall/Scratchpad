using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class StartupWindowStore
    {
        // What a fresh install gets; also what Settings' "Reset to defaults" restores.
        public static StartupWindowSettings Defaults { get; } = new StartupWindowSettings
        {
            ShowWindowOnStartup = false
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "startup-window.json");

        public string FilePath { get; }

        public StartupWindowStore(string filePath)
        {
            FilePath = filePath;
        }

        public StartupWindowSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<StartupWindowSettings>(json);
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(StartupWindowSettings settings)
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
