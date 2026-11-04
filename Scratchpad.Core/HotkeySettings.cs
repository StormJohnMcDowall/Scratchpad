using System;

namespace Scratchpad.Core
{
    // Values are Win32's MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN, so the Shell can hand them to
    // RegisterHotKey unchanged.
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    // What a key press means while the Settings box is recording a hotkey.
    public enum HotkeyKeyAction
    {
        KeepListening,
        Cancel,
        MoveFocus,
        NeedsModifier,
        Accept
    }

    public struct HotkeySettings : IEquatable<HotkeySettings>
    {
        public HotkeyModifiers Modifiers { get; set; }

        // A Win32 virtual-key code; 0 means no hotkey at all.
        public int VirtualKey { get; set; }

        public bool IsNone => VirtualKey == 0;

        public static HotkeySettings None => default(HotkeySettings);

        public bool Equals(HotkeySettings other)
        {
            return Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;
        }

        public override bool Equals(object obj)
        {
            return obj is HotkeySettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Modifiers << 16) ^ VirtualKey;
        }

        public static bool operator ==(HotkeySettings a, HotkeySettings b) => a.Equals(b);
        public static bool operator !=(HotkeySettings a, HotkeySettings b) => !a.Equals(b);
    }
}
