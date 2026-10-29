using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Scratchpad
{
    public static class FontCatalog
    {
        public static IReadOnlyList<FontFamily> MonospaceFamilies { get; } =
            Fonts.SystemFontFamilies.Where(IsMonospace).OrderBy(f => f.Source).ToList();

        public static IReadOnlyList<FontFamily> NormalTextFamilies { get; } =
            Fonts.SystemFontFamilies.Where(f => !IsSymbolFont(f)).OrderBy(f => f.Source).ToList();

        private static bool IsMonospace(FontFamily family)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphTypeface))
            {
                return false;
            }

            // 'i' and 'm' have very different advance widths in any proportional font and
            // identical ones in a true monospace font. TryGetValue (not the indexer) because a
            // font missing either glyph (e.g. a symbol or non-Latin-only font) must be excluded,
            // not thrown from — one unmeasurable font must never break the whole catalog.
            if (!glyphTypeface.CharacterToGlyphMap.TryGetValue('i', out var narrowGlyph) ||
                !glyphTypeface.CharacterToGlyphMap.TryGetValue('m', out var wideGlyph))
            {
                return false;
            }

            return Math.Abs(glyphTypeface.AdvanceWidths[narrowGlyph] - glyphTypeface.AdvanceWidths[wideGlyph]) < 0.001;
        }

        private static bool IsSymbolFont(FontFamily family)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            return typeface.TryGetGlyphTypeface(out var glyphTypeface) && glyphTypeface.Symbol;
        }
    }
}
