using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class FontFamilyStore
    {
        public const string DefaultEditorFontFamily = "Consolas";

        private static readonly FontFamilySettings DefaultSettings = new FontFamilySettings
        {
            EditorFontFamily = DefaultEditorFontFamily,
            PreviewNormalFontFamily = null,
            PreviewCodeFontFamily = null
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "font-family.json");

        public string FilePath { get; }

        public FontFamilyStore(string filePath)
        {
            FilePath = filePath;
        }

        public FontFamilySettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<FontFamilySettings>(json);
                return new FontFamilySettings
                {
                    EditorFontFamily = string.IsNullOrWhiteSpace(loaded.EditorFontFamily)
                        ? DefaultEditorFontFamily
                        : loaded.EditorFontFamily,
                    PreviewNormalFontFamily = string.IsNullOrWhiteSpace(loaded.PreviewNormalFontFamily)
                        ? null
                        : loaded.PreviewNormalFontFamily,
                    PreviewCodeFontFamily = string.IsNullOrWhiteSpace(loaded.PreviewCodeFontFamily)
                        ? null
                        : loaded.PreviewCodeFontFamily
                };
            }
            catch
            {
                return DefaultSettings;
            }
        }

        public void Save(FontFamilySettings settings)
        {
            // Font family, like window placement and font size, is a nicety rather than the
            // app's core promise — a failure here must never crash the app.
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
