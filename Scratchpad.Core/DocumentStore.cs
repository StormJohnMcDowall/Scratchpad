using System;
using System.IO;

namespace Scratchpad.Core
{
    public class DocumentStore
    {
        public static string DefaultFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "scratchpad.md");

        public string FilePath { get; }

        public DocumentStore(string filePath)
        {
            FilePath = filePath;
        }

        public string Load()
        {
            if (!File.Exists(FilePath))
            {
                Save("");
            }

            return File.ReadAllText(FilePath);
        }

        public void Save(string content)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, content);

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
