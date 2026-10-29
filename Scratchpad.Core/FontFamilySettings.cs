using System;

namespace Scratchpad.Core
{
    public struct FontFamilySettings : IEquatable<FontFamilySettings>
    {
        public string EditorFontFamily { get; set; }
        public string PreviewNormalFontFamily { get; set; }
        public string PreviewCodeFontFamily { get; set; }

        public bool Equals(FontFamilySettings other)
        {
            return string.Equals(EditorFontFamily, other.EditorFontFamily, StringComparison.Ordinal)
                && string.Equals(PreviewNormalFontFamily, other.PreviewNormalFontFamily, StringComparison.Ordinal)
                && string.Equals(PreviewCodeFontFamily, other.PreviewCodeFontFamily, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FontFamilySettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (EditorFontFamily?.GetHashCode() ?? 0);
                hash = hash * 31 + (PreviewNormalFontFamily?.GetHashCode() ?? 0);
                hash = hash * 31 + (PreviewCodeFontFamily?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static bool operator ==(FontFamilySettings a, FontFamilySettings b) => a.Equals(b);
        public static bool operator !=(FontFamilySettings a, FontFamilySettings b) => !a.Equals(b);
    }
}
