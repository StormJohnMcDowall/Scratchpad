using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class PeekStore
    {
        public const int MinSeconds = 1;
        public const int MaxSeconds = 30;

        // What a fresh install gets; also what Settings' "Reset to defaults" restores.
        public static PeekSettings Defaults { get; } = new PeekSettings
        {
            Seconds = 3
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "peek.json");

        public string FilePath { get; }

        public PeekStore(string filePath)
        {
            FilePath = filePath;
        }

        public static int Clamp(int seconds)
        {
            return Math.Max(MinSeconds, Math.Min(MaxSeconds, seconds));
        }

        /// <summary>
        /// Reads the Settings box: a whole number is pulled into range (so "0" peeks for 1 second
        /// and "45" for 30), anything else is null so the caller keeps the value it had.
        /// </summary>
        public static int? ParseSeconds(string text)
        {
            if (text == null || !long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return null;
            }

            return (int)Math.Max(MinSeconds, Math.Min(MaxSeconds, seconds));
        }

        public PeekSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<PeekSettings>(json);
                // A hand-edited file could hold 0 (a peek that never hides) or hours.
                return new PeekSettings { Seconds = Clamp(settings.Seconds) };
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(PeekSettings settings)
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
