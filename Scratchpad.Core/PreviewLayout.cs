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
        // Markdig.Wpf indents every list by a 40px left margin and draws the bullet in it, about
        // 27px in at the 14px default size; 1.9em puts the checkbox's left edge about there.
        public const double TaskListIndentEm = 1.9;

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

        // The list's left margin (not padding: Markdig.Wpf's list style puts the indent there).
        public static double TaskListLeftIndent(double fontSize) => fontSize * TaskListIndentEm;
    }
}
