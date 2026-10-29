using System;

namespace Scratchpad.Core
{
    public struct FontSizeSettings : IEquatable<FontSizeSettings>
    {
        public double EditorFontSize { get; set; }
        public double PreviewFontSize { get; set; }

        public bool Equals(FontSizeSettings other)
        {
            return EditorFontSize.Equals(other.EditorFontSize) && PreviewFontSize.Equals(other.PreviewFontSize);
        }

        public override bool Equals(object obj)
        {
            return obj is FontSizeSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + EditorFontSize.GetHashCode();
                hash = hash * 31 + PreviewFontSize.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(FontSizeSettings a, FontSizeSettings b) => a.Equals(b);
        public static bool operator !=(FontSizeSettings a, FontSizeSettings b) => !a.Equals(b);
    }
}
