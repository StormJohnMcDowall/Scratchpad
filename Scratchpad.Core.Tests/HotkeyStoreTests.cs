using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class HotkeyStoreTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "hotkey.json");

    [Fact]
    public void Defaults_IsWinShiftN()
    {
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Shift, HotkeyStore.Defaults.Modifiers);
        Assert.Equal(0x4E, HotkeyStore.Defaults.VirtualKey);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        Assert.Equal(HotkeyStore.Defaults, new HotkeyStore(NewPath()).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");

        Assert.Equal(HotkeyStore.Defaults, new HotkeyStore(path).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = NewPath();
        var hotkey = new HotkeySettings { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x74 };

        new HotkeyStore(path).Save(hotkey);

        Assert.Equal(hotkey, new HotkeyStore(path).Load());
    }

    // A hotkey the user turned off must stay off, not come back as the default.
    [Fact]
    public void SaveThenLoad_None_StaysNone()
    {
        var path = NewPath();

        new HotkeyStore(path).Save(HotkeySettings.None);

        Assert.True(new HotkeyStore(path).Load().IsNone);
    }

    [Fact]
    public void Load_NoneWithStrayModifiers_IsPlainNone()
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "{\"Modifiers\":8,\"VirtualKey\":0}");

        Assert.Equal(HotkeySettings.None, new HotkeyStore(path).Load());
    }

    [Theory]
    [InlineData("{\"Modifiers\":4,\"VirtualKey\":65}")]
    [InlineData("{\"Modifiers\":0,\"VirtualKey\":65}")]
    [InlineData("{\"Modifiers\":2,\"VirtualKey\":17}")]
    [InlineData("{\"Modifiers\":8,\"VirtualKey\":999}")]
    [InlineData("{\"Modifiers\":8,\"VirtualKey\":-4}")]
    [InlineData("{\"Modifiers\":24,\"VirtualKey\":78}")]
    [InlineData("{\"Modifiers\":-1,\"VirtualKey\":78}")]
    public void Load_InvalidHotkey_ReturnsDefaults(string json)
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);

        Assert.Equal(HotkeyStore.Defaults, new HotkeyStore(path).Load());
    }
}
