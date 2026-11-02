using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class FontSizeStore
    {
        // What a fresh install gets; also what Settings' "Reset to defaults" restores.
        public static FontSizeSettings Defaults { get; } = new FontSizeSettings
        {
            EditorFontSize = FontSizing.Default,
            PreviewFontSize = FontSizing.Default
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "font-size.json");

        public string FilePath { get; }

        public FontSizeStore(string filePath)
        {
            FilePath = filePath;
        }

        public FontSizeSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<FontSizeSettings>(json);
                return new FontSizeSettings
                {
                    EditorFontSize = FontSizing.Clamp(loaded.EditorFontSize),
                    PreviewFontSize = FontSizing.Clamp(loaded.PreviewFontSize)
                };
            }
            catch
            {
                return Defaults;
            }
        }

        public void Save(FontSizeSettings settings)
        {
            // Font size, like window placement, is a nicety rather than the app's core promise
            // — a failure here (e.g. non-finite values, a locked file) must never crash the app.
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
