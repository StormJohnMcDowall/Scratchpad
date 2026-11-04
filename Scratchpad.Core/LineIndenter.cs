using System.Text;

namespace Scratchpad.Core
{
    /// <summary>
    /// One edit produced by <see cref="LineIndenter"/>: replace the given range (whole lines,
    /// excluding the last one's line break) with <see cref="Replacement"/>, then select the given
    /// range of the edited text.
    /// </summary>
    public sealed class IndentEdit
    {
        public IndentEdit(int replaceStart, int replaceLength, string replacement, int selectionStart, int selectionLength)
        {
            ReplaceStart = replaceStart;
            ReplaceLength = replaceLength;
            Replacement = replacement;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
        }

        public int ReplaceStart { get; }
        public int ReplaceLength { get; }
        public string Replacement { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
    }

    /// <summary>
    /// Block indent (Tab) and unindent (Shift+Tab) over the logical lines a selection touches.
    /// Lines are split on '\n'; a '\r' before it is treated as part of the line's content, so
    /// both "\r\n" and "\n" text work.
    /// </summary>
    public static class LineIndenter
    {
        public const string IndentUnit = "    ";

        /// <summary>
        /// Indents every non-empty line the selection touches by <see cref="IndentUnit"/>.
        /// Returns null when the selection doesn't span a line break, so the caller can let Tab
        /// type as usual.
        /// </summary>
        public static IndentEdit Indent(string text, int selectionStart, int selectionLength)
        {
            if (text.IndexOf('\n', selectionStart, selectionLength) < 0)
            {
                return null;
            }

            return Edit(text, selectionStart, selectionLength, IndentLine);
        }

        /// <summary>
        /// Removes one leading tab, or else up to four leading spaces, from every line the
        /// selection (or just the caret) touches. Returns null when there's nothing to remove.
        /// </summary>
        public static IndentEdit Outdent(string text, int selectionStart, int selectionLength)
        {
            return Edit(text, selectionStart, selectionLength, OutdentLine);
        }

        private delegate string LineEdit(string line, out int removed);

        private static string IndentLine(string line, out int removed)
        {
            removed = 0;
            // Empty lines are left alone so indenting doesn't litter them with trailing spaces.
            return line.Length == 0 || line == "\r" ? line : IndentUnit + line;
        }

        private static string OutdentLine(string line, out int removed)
        {
            if (line.StartsWith("\t"))
            {
                removed = 1;
            }
            else
            {
                removed = 0;
                while (removed < IndentUnit.Length && removed < line.Length && line[removed] == ' ')
                {
                    removed++;
                }
            }

            return line.Substring(removed);
        }

        private static IndentEdit Edit(string text, int selectionStart, int selectionLength, LineEdit lineEdit)
        {
            var selectionEnd = selectionStart + selectionLength;
            var firstLineStart = selectionStart == 0 ? 0 : text.LastIndexOf('\n', selectionStart - 1) + 1;

            // A selection ending just after a line break (e.g. from Shift+Down) doesn't touch
            // the line that follows it.
            var endsAtLineStart = selectionLength > 0 && text[selectionEnd - 1] == '\n';
            var lastTouched = endsAtLineStart ? selectionEnd - 1 : selectionEnd;

            var blockEnd = text.IndexOf('\n', lastTouched);
            if (blockEnd < 0)
            {
                blockEnd = text.Length;
            }
            if (blockEnd > firstLineStart && text[blockEnd - 1] == '\r')
            {
                blockEnd--;
            }

            var lines = text.Substring(firstLineStart, blockEnd - firstLineStart).Split('\n');
            var replacement = new StringBuilder();
            var firstLineRemoved = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    replacement.Append('\n');
                }
                replacement.Append(lineEdit(lines[i], out var removed));
                if (i == 0)
                {
                    firstLineRemoved = removed;
                }
            }

            var newBlock = replacement.ToString();
            var blockLength = blockEnd - firstLineStart;
            // Lines only ever gain or lose leading characters, so an unchanged length is no change.
            if (newBlock.Length == blockLength)
            {
                return null;
            }

            if (lines.Length == 1 && !endsAtLineStart)
            {
                // Caret, or a selection within one line: keep each end on the same text, clamped
                // to the line start if it sat inside the indent that was removed.
                var start = ShiftLeft(selectionStart, firstLineStart, firstLineRemoved);
                var end = ShiftLeft(selectionEnd, firstLineStart, firstLineRemoved);
                return new IndentEdit(firstLineStart, blockLength, newBlock, start, end - start);
            }

            // Several lines: select them whole, so the next Tab or Shift+Tab acts on the same lines.
            var newSelectionEnd = endsAtLineStart
                ? selectionEnd + newBlock.Length - blockLength
                : firstLineStart + newBlock.Length;
            return new IndentEdit(firstLineStart, blockLength, newBlock, firstLineStart, newSelectionEnd - firstLineStart);
        }

        private static int ShiftLeft(int position, int lineStart, int removed)
        {
            return position - lineStart >= removed ? position - removed : lineStart;
        }
    }
}
