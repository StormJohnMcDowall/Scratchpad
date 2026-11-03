using System;

namespace Scratchpad.Core
{
    public struct DateFormatSettings : IEquatable<DateFormatSettings>
    {
        public string Format { get; set; }

        public bool Equals(DateFormatSettings other)
        {
            return string.Equals(Format, other.Format, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DateFormatSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Format == null ? 0 : StringComparer.Ordinal.GetHashCode(Format);
        }

        public static bool operator ==(DateFormatSettings a, DateFormatSettings b) => a.Equals(b);
        public static bool operator !=(DateFormatSettings a, DateFormatSettings b) => !a.Equals(b);
    }
}
