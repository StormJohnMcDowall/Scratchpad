using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scratchpad.Core
{
    public enum BackupKind
    {
        Regular,
        BeforeBigDelete
    }

    /// <summary>
    /// The decisions behind the note's backup copies, kept free of the file system and the
    /// clock ("now" is always passed in) so every boundary can be tested.
    /// </summary>
    public static class BackupRules
    {
        private const string Prefix = "scratchpad ";
        private const string RegularSuffix = ".md";
        private const string BigDeleteSuffix = " before big delete.md";
        private const string TimeFormat = "yyyy-MM-dd HHmmss";

        private const int BigDeleteAlways = 1000;
        private const int HalfNoteFloor = 100;
        private static readonly TimeSpan HourlyInterval = TimeSpan.FromHours(1);
        private static readonly TimeSpan ClockSetBackSlack = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan KeepEverything = TimeSpan.FromHours(48);
        private const int KeepDailyDays = 30;

        /// <summary>
        /// How much of the before-text one edit removed: whatever is left once the common start
        /// and end are dropped. Text added by the same edit doesn't offset it, so pasting over a
        /// selection counts everything that was selected.
        /// </summary>
        public static int RemovedLength(string before, string after)
        {
            var shorter = Math.Min(before.Length, after.Length);

            var prefix = 0;
            while (prefix < shorter && before[prefix] == after[prefix])
            {
                prefix++;
            }

            // Capped so the common end never re-counts characters the common start already took
            // ("aaaa" → "aa" removed 2, not 0).
            var suffix = 0;
            while (suffix < shorter - prefix
                && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
            {
                suffix++;
            }

            return before.Length - prefix - suffix;
        }

        /// <summary>
        /// At least 1,000 characters gone, or at least half a note of 100+ characters — the
        /// slips (select all + delete, paste over everything) a copy should be taken before.
        /// </summary>
        public static bool IsBigDelete(string before, string after)
        {
            var removed = RemovedLength(before, after);
            return removed >= BigDeleteAlways
                || (before.Length >= HalfNoteFloor && removed * 2 >= before.Length);
        }

        // A newest copy well "in the future" means the clock was set back since (or DST fell
        // back); waiting for real time to catch up would silently stop hourly copies, so it's
        // due. The minute's slack keeps a racing autosave callback (milliseconds behind the copy
        // just written) from counting as a clock change.
        public static bool IsHourlyCopyDue(DateTime? newestRegular, DateTime now)
        {
            return newestRegular == null
                || newestRegular.Value - now > ClockSetBackSlack
                || now - newestRegular.Value >= HourlyInterval;
        }

        public static string FileName(DateTime time, BackupKind kind)
        {
            var stamp = time.ToString(TimeFormat, CultureInfo.InvariantCulture);
            return Prefix + stamp + (kind == BackupKind.BeforeBigDelete ? BigDeleteSuffix : RegularSuffix);
        }

        /// <summary>
        /// Recognises only names this app writes; anything else in the folder is the user's and
        /// is never touched.
        /// </summary>
        public static bool TryParseFileName(string fileName, out DateTime time, out BackupKind kind)
        {
            time = default(DateTime);
            kind = BackupKind.Regular;
            if (fileName == null || !fileName.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string stamp;
            if (fileName.EndsWith(BigDeleteSuffix, StringComparison.Ordinal))
            {
                kind = BackupKind.BeforeBigDelete;
                stamp = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - BigDeleteSuffix.Length);
            }
            else if (fileName.EndsWith(RegularSuffix, StringComparison.Ordinal))
            {
                stamp = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - RegularSuffix.Length);
            }
            else
            {
                return false;
            }

            return DateTime.TryParseExact(stamp, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
        }

        /// <summary>
        /// Which copies to delete: everything from the last 48 hours stays; before that, the
        /// newest copy of each local calendar day back to 30 days before today stays; older
        /// copies go. Names that aren't this app's copies are never included.
        /// </summary>
        public static IReadOnlyList<string> FilesToDelete(IEnumerable<string> fileNames, DateTime now)
        {
            var cutoffDay = now.Date.AddDays(-KeepDailyDays);
            var keepAllFrom = now - KeepEverything;
            var toDelete = new List<string>();

            var older = new List<KeyValuePair<string, DateTime>>();
            foreach (var name in fileNames)
            {
                if (!TryParseFileName(name, out var time, out _) || time >= keepAllFrom)
                {
                    continue;
                }
                if (time.Date < cutoffDay)
                {
                    toDelete.Add(name);
                }
                else
                {
                    older.Add(new KeyValuePair<string, DateTime>(name, time));
                }
            }

            foreach (var day in older.GroupBy(copy => copy.Value.Date))
            {
                toDelete.AddRange(day.OrderByDescending(copy => copy.Value).Skip(1).Select(copy => copy.Key));
            }

            return toDelete;
        }
    }
}
