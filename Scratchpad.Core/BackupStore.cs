using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Scratchpad.Core
{
    /// <summary>
    /// Dated copies of the note, for taking text back by hand after a slip. Best-effort
    /// throughout: every method swallows its failures and just reports whether a copy was
    /// written — a backup must never get in the way of the note itself. Thread-safe, since
    /// hourly copies come from the autosave's timer thread and the rest from the UI thread.
    /// </summary>
    public class BackupStore
    {
        public static string DefaultFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "backups");

        private readonly object _lock = new object();
        private bool _hasLookedForNewest;
        // The newest *regular* copy: before-big-delete copies hold old text, so they neither
        // restart the hourly clock nor count as the latest text.
        private DateTime? _newestRegularTime;
        private string _newestRegularText;

        public string Folder { get; }

        public BackupStore(string folder)
        {
            Folder = folder;
        }

        public bool TryWriteRegularIfDue(string text, DateTime now)
        {
            lock (_lock)
            {
                LookForNewestRegular();
                if (!BackupRules.IsHourlyCopyDue(_newestRegularTime, now) || text == _newestRegularText)
                {
                    return false;
                }
                return TryWriteRegular(text, now);
            }
        }

        public bool TryWriteOnExit(string text, DateTime now)
        {
            lock (_lock)
            {
                LookForNewestRegular();
                if (text == _newestRegularText)
                {
                    return false;
                }
                return TryWriteRegular(text, now);
            }
        }

        // Never overwrites: if two big deletes land in the same second, the first copy is the
        // one holding the real old note.
        public bool TryWriteBeforeBigDelete(string oldText, DateTime now)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    var path = Path.Combine(Folder, BackupRules.FileName(now, BackupKind.BeforeBigDelete));
                    using (var stream = new FileStream(path, FileMode.CreateNew))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(oldText);
                    }
                }
                catch
                {
                    return false;
                }

                Prune(now);
                return true;
            }
        }

        private bool TryWriteRegular(string text, DateTime now)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, BackupRules.FileName(now, BackupKind.Regular)), text);
            }
            catch
            {
                return false;
            }

            _newestRegularTime = now;
            _newestRegularText = text;
            Prune(now);
            return true;
        }

        // Once per run, so a restart remembers the last copy instead of writing another at once.
        private void LookForNewestRegular()
        {
            if (_hasLookedForNewest)
            {
                return;
            }
            _hasLookedForNewest = true;

            try
            {
                if (!Directory.Exists(Folder))
                {
                    return;
                }

                var newest = Directory.GetFiles(Folder)
                    .Select(path => BackupRules.TryParseFileName(Path.GetFileName(path), out var time, out var kind)
                        ? new { Path = path, Time = (DateTime?)time, Kind = kind }
                        : null)
                    .Where(copy => copy != null && copy.Kind == BackupKind.Regular)
                    .OrderByDescending(copy => copy.Time)
                    .FirstOrDefault();
                if (newest != null)
                {
                    _newestRegularText = File.ReadAllText(newest.Path);
                    _newestRegularTime = newest.Time;
                }
            }
            catch
            {
                // Unknown newest copy: at worst one extra copy gets written.
                _newestRegularTime = null;
                _newestRegularText = null;
            }
        }

        private void Prune(DateTime now)
        {
            try
            {
                var names = Directory.GetFiles(Folder).Select(Path.GetFileName);
                foreach (var name in BackupRules.FilesToDelete(names, now))
                {
                    try
                    {
                        File.Delete(Path.Combine(Folder, name));
                    }
                    catch
                    {
                        // Left for the next prune.
                    }
                }
            }
            catch
            {
                // Pruning is housekeeping; the copy itself was written.
            }
        }
    }
}
