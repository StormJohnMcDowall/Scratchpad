using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class DateFormatStore
    {
        // What a fresh install gets; also what Settings' "Reset to defaults" restores.
        public static DateFormatSettings Defaults { get; } = new DateFormatSettings
        {
            Format = DateStamp.DefaultFormat
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "date-format.json");

        public string FilePath { get; }

        public DateFormatStore(string filePath)
        {
            FilePath = filePath;
        }

        public DateFormatSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<DateFormatSettings>(json);
                // A blank format would otherwise reopen Settings with an empty box.
                return string.IsNullOrWhiteSpace(settings.Format) ? Defaults : settings;
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(DateFormatSettings settings)
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
