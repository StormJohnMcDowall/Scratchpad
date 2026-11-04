using Scratchpad.Core;
using Xunit;

public class HotkeyTests
{
    [Fact]
    public void None_IsNone()
    {
        Assert.True(HotkeySettings.None.IsNone);
    }

    // The Shell passes these straight to RegisterHotKey as MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN.
    [Fact]
    public void Modifiers_MatchWin32Values()
    {
        Assert.Equal(1, (int)HotkeyModifiers.Alt);
        Assert.Equal(2, (int)HotkeyModifiers.Control);
        Assert.Equal(4, (int)HotkeyModifiers.Shift);
        Assert.Equal(8, (int)HotkeyModifiers.Win);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, "Win+Shift+N")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x74, "Ctrl+Alt+F5")]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Win, 0x20, "Win+Ctrl+Alt+Shift+Space")]
    [InlineData(HotkeyModifiers.Control, 0x63, "Ctrl+Num 3")]
    [InlineData(HotkeyModifiers.Alt, 0xBA, "Alt+;")]
    [InlineData(HotkeyModifiers.Win, 0xE7, "Win+Key 0xE7")]
    public void Format_NamesModifiersInOrderThenKey(HotkeyModifiers mods, int vk, string expected)
    {
        Assert.Equal(expected, Hotkey.Format(new HotkeySettings { Modifiers = mods, VirtualKey = vk }));
    }

    [Fact]
    public void Format_None_IsNone()
    {
        Assert.Equal("None", Hotkey.Format(HotkeySettings.None));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, true)]
    [InlineData(HotkeyModifiers.Control, 0x41, true)]
    [InlineData(HotkeyModifiers.Alt, 0x70, true)]
    [InlineData(HotkeyModifiers.Shift, 0x41, false)]
    [InlineData(HotkeyModifiers.None, 0x41, false)]
    [InlineData(HotkeyModifiers.Control, 0x10, false)]
    [InlineData(HotkeyModifiers.Win, 0xA2, false)]
    [InlineData(HotkeyModifiers.Win, 0, false)]
    [InlineData(HotkeyModifiers.Win, 0xFF, false)]
    [InlineData(HotkeyModifiers.Win, -1, false)]
    [InlineData((HotkeyModifiers)0x18, 0x4E, false)]
    [InlineData((HotkeyModifiers)(-1), 0x4E, false)]
    public void IsValid_RequiresWinCtrlOrAltAndARealKey(HotkeyModifiers mods, int vk, bool expected)
    {
        Assert.Equal(expected, Hotkey.IsValid(new HotkeySettings { Modifiers = mods, VirtualKey = vk }));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Win, 0x5B, HotkeyKeyAction.KeepListening)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0xA0, HotkeyKeyAction.KeepListening)]
    [InlineData(HotkeyModifiers.None, 0x1B, HotkeyKeyAction.Cancel)]
    [InlineData(HotkeyModifiers.Control, 0x1B, HotkeyKeyAction.Accept)]
    [InlineData(HotkeyModifiers.None, 0x09, HotkeyKeyAction.MoveFocus)]
    [InlineData(HotkeyModifiers.Shift, 0x09, HotkeyKeyAction.MoveFocus)]
    [InlineData(HotkeyModifiers.Control, 0x09, HotkeyKeyAction.Accept)]
    [InlineData(HotkeyModifiers.None, 0x41, HotkeyKeyAction.NeedsModifier)]
    [InlineData(HotkeyModifiers.Shift, 0x41, HotkeyKeyAction.NeedsModifier)]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x4E, HotkeyKeyAction.Accept)]
    public void Classify_FollowsCaptureRules(HotkeyModifiers mods, int vk, HotkeyKeyAction expected)
    {
        Assert.Equal(expected, Hotkey.Classify(mods, vk));
    }
}
