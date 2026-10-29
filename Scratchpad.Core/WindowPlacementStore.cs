using System;
using System.IO;
using System.Text.Json;

namespace Scratchpad.Core
{
    public class WindowPlacementStore
    {
        private static readonly WindowBounds DefaultBounds = new WindowBounds
        {
            Left = 100,
            Top = 100,
            Width = 420,
            Height = 520
        };

        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "window-placement.json");

        public string FilePath { get; }

        public WindowPlacementStore(string filePath)
        {
            FilePath = filePath;
        }

        public WindowBounds Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<WindowBounds>(json);
            }
            catch
            {
                return DefaultBounds;
            }
        }

        public void Save(WindowBounds bounds)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(bounds));

            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, null);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }
        }
    }
}
