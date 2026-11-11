using System.Collections.Generic;

namespace Scratchpad.Core
{
    public readonly struct VerticalMargin
    {
        public VerticalMargin(double top, double bottom)
        {
            Top = top;
            Bottom = bottom;
        }

        public double Top { get; }

        public double Bottom { get; }
    }

    // Spacing for the rendered preview. WPF's default paragraph spacing is about one line of the
    // paragraph's own font, so Markdig.Wpf's big headings get big gaps on both sides. These rules
    // replace that with a moderate gap above and a small one below. Every value is in ems (a
    // multiple of the element's own font size): the preview zoom is a LayoutTransform that scales
    // everything, so em-based spacing stays in proportion at any zoom without extra work.
    public static class PreviewLayout
    {
        public const double HeadingTopEm = 0.6;
        public const double HeadingBottomEm = 0.15;
        // Measured on Windows: at the 14px default size a bullet sits about 20px in from the text
        // edge, so 1.4em starts the checkbox about where the bullet was.
        public const double TaskListIndentEm = 1.4;

        // The note's first block gets no gap above it, so a heading at the top sits at the top.
        public static VerticalMargin HeadingMargin(double fontSize, bool isFirstBlock) =>
            new VerticalMargin(isFirstBlock ? 0 : fontSize * HeadingTopEm, fontSize * HeadingBottomEm);

        // WPF sets the bullet style per list, not per item, so bullets go only when every item is
        // a task; a mixed list keeps them so its plain items still have a marker.
        public static bool IsTaskList(IReadOnlyList<bool> itemStartsWithCheckbox)
        {
            if (itemStartsWithCheckbox.Count == 0)
            {
                return false;
            }

            foreach (var isTask in itemStartsWithCheckbox)
            {
                if (!isTask)
                {
                    return false;
                }
            }

            return true;
        }

        // Numbered lists keep their numbers even when every item is a task: the order may matter.
        public static bool HidesMarkers(bool isNumbered, IReadOnlyList<bool> itemStartsWithCheckbox) =>
            !isNumbered && IsTaskList(itemStartsWithCheckbox);

        // H1 relative to body text; Markdig.Wpf's own H1 is far bigger and dwarfs a small note.
        public const double Heading1Em = 1.8;

        // Code (Consolas by default) has taller lowercase letters than the body font, so at the
        // same size it looks a size up; 0.9em makes the two look matched.
        public const double CodeEm = 0.9;

        public static double Heading1Size(double bodyFontSize) => bodyFontSize * Heading1Em;

        public static double CodeSize(double surroundingFontSize) => surroundingFontSize * CodeEm;

        // Whatever sits straight under a heading (list, paragraph, code, quote) sits on it, as the
        // heading's small bottom gap intends. Otherwise its automatic top margin (NaN, about one
        // line) would win, since touching margins collapse to the larger. Elsewhere: unchanged.
        public static double TopMarginUnder(bool followsHeading, double currentTop) =>
            followsHeading ? 0 : currentTop;

        // The list's whole left indent (the Shell puts it in Padding and zeroes the left Margin).
        public static double TaskListLeftIndent(double fontSize) => fontSize * TaskListIndentEm;
    }
}
