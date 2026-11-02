using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class ThemeStoreTests
{
    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.json");

        var loaded = new ThemeStore(path).Load();

        Assert.Equal(ThemeStore.Defaults, loaded);
        Assert.Equal(ThemePreference.FollowWindows, loaded.Preference);
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");

        Assert.Equal(ThemePreference.FollowWindows, new ThemeStore(path).Load().Preference);
    }

    [Fact]
    public void Load_ReturnsDefaultWhenPreferenceOutOfRange()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"Preference\":7}");

        Assert.Equal(ThemePreference.FollowWindows, new ThemeStore(path).Load().Preference);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsDark()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        new ThemeStore(path).Save(new ThemeSettings { Preference = ThemePreference.Dark });

        Assert.Equal(ThemePreference.Dark, new ThemeStore(path).Load().Preference);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.json");

        new ThemeStore(path).Save(new ThemeSettings { Preference = ThemePreference.Light });

        Assert.True(File.Exists(path));
    }
}
