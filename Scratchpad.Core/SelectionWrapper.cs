namespace Scratchpad.Core
{
    /// <summary>
    /// Typing a markdown emphasis character over a selection wraps the selection in it instead
    /// of replacing it, and keeps the original text selected so typing it again stacks
    /// (<c>*</c> twice gives bold).
    /// </summary>
    public static class SelectionWrapper
    {
        public const string WrapCharacters = "*_`~";

        /// <summary>
        /// Returns the wrap edit, or null when the caller should let <paramref name="typed"/>
        /// type as usual: no selection, not exactly one wrap character, or a selection that is
        /// only whitespace. Leading and trailing whitespace is kept outside the wrap: a
        /// double-clicked word comes with its trailing space and Shift+Down with a line break,
        /// and markdown doesn't treat <c>*word *</c> or <c>*abc</c> + break + <c>*</c> as emphasis.
        /// </summary>
        public static IndentEdit Wrap(string text, int selectionStart, int selectionLength, string typed)
        {
            if (selectionLength == 0 || typed == null || typed.Length != 1 || WrapCharacters.IndexOf(typed[0]) < 0)
            {
                return null;
            }

            var start = selectionStart;
            var end = selectionStart + selectionLength;
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
            while (end > start && char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }
            if (start == end)
            {
                return null;
            }

            var length = end - start;
            var inner = text.Substring(start, length);
            return new IndentEdit(start, length, typed + inner + typed, start + 1, length);
        }
    }
}
