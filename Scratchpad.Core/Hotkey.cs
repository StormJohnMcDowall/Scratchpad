using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Scratchpad.Core
{
    public static class Hotkey
    {
        private const HotkeyModifiers RequiredModifiers = HotkeyModifiers.Win | HotkeyModifiers.Control | HotkeyModifiers.Alt;
        private const HotkeyModifiers KnownModifiers = RequiredModifiers | HotkeyModifiers.Shift;
        private const int VkTab = 0x09;
        private const int VkEscape = 0x1B;

        // Punctuation is named by its US key cap; on a UK keyboard a couple of these show a
        // different symbol than the key is printed with — accepted, since they're rare hotkeys.
        private static readonly Dictionary<int, string> KeyNames = new Dictionary<int, string>
        {
            { 0x08, "Backspace" }, { 0x09, "Tab" }, { 0x0D, "Enter" }, { 0x13, "Pause" }, { 0x1B, "Esc" },
            { 0x20, "Space" }, { 0x21, "PageUp" }, { 0x22, "PageDown" }, { 0x23, "End" }, { 0x24, "Home" },
            { 0x25, "Left" }, { 0x26, "Up" }, { 0x27, "Right" }, { 0x28, "Down" },
            { 0x2C, "PrintScreen" }, { 0x2D, "Insert" }, { 0x2E, "Delete" },
            { 0xBA, ";" }, { 0xBB, "=" }, { 0xBC, "," }, { 0xBD, "-" }, { 0xBE, "." }, { 0xBF, "/" },
            { 0xC0, "`" }, { 0xDB, "[" }, { 0xDC, "\\" }, { 0xDD, "]" }, { 0xDE, "'" }
        };

        /// <summary>
        /// Whether the hotkey can be registered: a real, non-modifier key with Win, Ctrl or Alt —
        /// a plain or Shift-only letter would be stolen from typing in every app. Unknown modifier
        /// bits (only a hand-edited file has them) are invalid too: Windows would refuse them.
        /// </summary>
        public static bool IsValid(HotkeySettings hotkey)
        {
            return hotkey.VirtualKey >= 1 && hotkey.VirtualKey <= 0xFE
                && !IsModifierKey(hotkey.VirtualKey)
                && (hotkey.Modifiers & ~KnownModifiers) == 0
                && (hotkey.Modifiers & RequiredModifiers) != 0;
        }

        // Shift, Ctrl, Alt and the Win keys, generic and left/right forms.
        public static bool IsModifierKey(int virtualKey)
        {
            return (virtualKey >= 0x10 && virtualKey <= 0x12)
                || virtualKey == 0x5B || virtualKey == 0x5C
                || (virtualKey >= 0xA0 && virtualKey <= 0xA5);
        }

        public static HotkeyKeyAction Classify(HotkeyModifiers modifiers, int virtualKey)
        {
            if (IsModifierKey(virtualKey))
            {
                return HotkeyKeyAction.KeepListening;
            }
            if (virtualKey == VkEscape && modifiers == HotkeyModifiers.None)
            {
                return HotkeyKeyAction.Cancel;
            }

            var hasRequired = (modifiers & RequiredModifiers) != 0;
            if (virtualKey == VkTab && !hasRequired)
            {
                return HotkeyKeyAction.MoveFocus;
            }
            return hasRequired ? HotkeyKeyAction.Accept : HotkeyKeyAction.NeedsModifier;
        }

        public static string Format(HotkeySettings hotkey)
        {
            if (hotkey.IsNone)
            {
                return "None";
            }

            var text = new StringBuilder();
            if ((hotkey.Modifiers & HotkeyModifiers.Win) != 0) text.Append("Win+");
            if ((hotkey.Modifiers & HotkeyModifiers.Control) != 0) text.Append("Ctrl+");
            if ((hotkey.Modifiers & HotkeyModifiers.Alt) != 0) text.Append("Alt+");
            if ((hotkey.Modifiers & HotkeyModifiers.Shift) != 0) text.Append("Shift+");
            return text.Append(KeyName(hotkey.VirtualKey)).ToString();
        }

        public static string KeyName(int virtualKey)
        {
            if ((virtualKey >= 0x41 && virtualKey <= 0x5A) || (virtualKey >= 0x30 && virtualKey <= 0x39))
            {
                return ((char)virtualKey).ToString();
            }
            if (virtualKey >= 0x70 && virtualKey <= 0x87)
            {
                return "F" + (virtualKey - 0x6F).ToString(CultureInfo.InvariantCulture);
            }
            if (virtualKey >= 0x60 && virtualKey <= 0x69)
            {
                return "Num " + (virtualKey - 0x60).ToString(CultureInfo.InvariantCulture);
            }
            return KeyNames.TryGetValue(virtualKey, out var name)
                ? name
                : "Key 0x" + virtualKey.ToString("X2", CultureInfo.InvariantCulture);
        }
    }
}
