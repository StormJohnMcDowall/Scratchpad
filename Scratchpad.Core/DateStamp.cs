using System;

namespace Scratchpad.Core
{
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
            if (string.IsNullOrWhiteSpace(format))
            {
                return date.ToString(DefaultFormat, culture);
            }

            try
            {
                return date.ToString(format, culture);
            }
            catch (FormatException)
            {
                return date.ToString(DefaultFormat, culture);
            }
        }
    }
}
