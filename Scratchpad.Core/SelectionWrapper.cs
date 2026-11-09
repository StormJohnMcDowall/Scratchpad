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
        /// only a line break. A trailing line break ("\n" or "\r\n", as Shift+Down leaves) is
        /// kept outside the wrap, since <c>*abc</c> + break + <c>*</c> isn't emphasis.
        /// </summary>
        public static IndentEdit Wrap(string text, int selectionStart, int selectionLength, string typed)
        {
            if (selectionLength == 0 || typed == null || typed.Length != 1 || WrapCharacters.IndexOf(typed[0]) < 0)
            {
                return null;
            }

            var length = selectionLength;
            if (text[selectionStart + length - 1] == '\n')
            {
                length--;
                if (length > 0 && text[selectionStart + length - 1] == '\r')
                {
                    length--;
                }
            }
            if (length == 0)
            {
                return null;
            }

            var inner = text.Substring(selectionStart, length);
            return new IndentEdit(selectionStart, length, typed + inner + typed, selectionStart + 1, length);
        }
    }
}
