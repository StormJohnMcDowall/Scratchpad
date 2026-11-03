using System;

namespace Scratchpad.Core
{
    /// <summary>How DateStamp.Format treated the format string it was given.</summary>
    public enum DateFormatStatus
    {
        /// <summary>The format was used as given.</summary>
        Valid,

        /// <summary>The format was empty or whitespace, so DefaultFormat was used.</summary>
        Blank,

        /// <summary>.NET rejected the format, so DefaultFormat was used.</summary>
        Rejected
    }

    public static class DateStamp
    {
        // "Tue 3 Nov": short day, day of month, short month.
        public const string DefaultFormat = "ddd d MMM";

        /// <summary>
        /// Formats the date with a .NET date format string typed into Settings. A blank format, or
        /// one .NET rejects (e.g. "%" alone or a trailing backslash), falls back to DefaultFormat
        /// so inserting a date or showing the Settings example can never throw.
        /// </summary>
        public static string Format(string format, DateTime date, IFormatProvider culture)
        {
            return Format(format, date, culture, out _);
        }

        /// <summary>
        /// As Format, also reporting whether the format was used or the default stood in for it,
        /// so Settings can say why the example doesn't match what was typed.
        /// </summary>
        public static string Format(string format, DateTime date, IFormatProvider culture, out DateFormatStatus status)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                status = DateFormatStatus.Blank;
                return date.ToString(DefaultFormat, culture);
            }

            try
            {
                status = DateFormatStatus.Valid;
                return date.ToString(format, culture);
            }
            catch (FormatException)
            {
                status = DateFormatStatus.Rejected;
                return date.ToString(DefaultFormat, culture);
            }
        }
    }
}
