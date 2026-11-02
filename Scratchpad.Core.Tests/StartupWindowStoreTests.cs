using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class StartupWindowStoreTests
{
    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "startup-window.json");

        Assert.Equal(StartupWindowStore.Defaults, new StartupWindowStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");

        Assert.False(new StartupWindowStore(path).Load().ShowWindowOnStartup);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "startup-window.json");

        new StartupWindowStore(path).Save(new StartupWindowSettings { ShowWindowOnStartup = true });

        Assert.True(new StartupWindowStore(path).Load().ShowWindowOnStartup);
    }

    [Fact]
    public void Defaults_IsHidden()
    {
        Assert.False(StartupWindowStore.Defaults.ShowWindowOnStartup);
    }
}
