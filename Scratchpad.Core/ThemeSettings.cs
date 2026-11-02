using System;

namespace Scratchpad.Core
{
    public struct ThemeSettings : IEquatable<ThemeSettings>
    {
        public ThemePreference Preference { get; set; }

        public bool Equals(ThemeSettings other)
        {
            return Preference == other.Preference;
        }

        public override bool Equals(object obj)
        {
            return obj is ThemeSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Preference.GetHashCode();
        }

        public static bool operator ==(ThemeSettings a, ThemeSettings b) => a.Equals(b);
        public static bool operator !=(ThemeSettings a, ThemeSettings b) => !a.Equals(b);
    }
}
