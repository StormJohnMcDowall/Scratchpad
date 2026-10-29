using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class WindowPlacementTests
{
    static readonly WindowBounds Screen = new WindowBounds { Left = 0, Top = 0, Width = 1920, Height = 1080 };

    [Fact]
    public void Clamp_LeavesOnScreenRectUnchanged()
    {
        var rect = new WindowBounds { Left = 100, Top = 100, Width = 420, Height = 520 };
        Assert.Equal(rect, WindowPlacement.Clamp(rect, Screen));
    }

    [Fact]
    public void Clamp_MovesFullyOffscreenRectBackOntoScreen()
    {
        var rect = new WindowBounds { Left = -5000, Top = -5000, Width = 420, Height = 520 };

        var result = WindowPlacement.Clamp(rect, Screen);

        Assert.InRange(result.Left, Screen.Left, Screen.Left + Screen.Width - result.Width);
        Assert.InRange(result.Top, Screen.Top, Screen.Top + Screen.Height - result.Height);
    }

    [Fact]
    public void Clamp_AlignsToTopLeftWhenLargerThanScreen()
    {
        var rect = new WindowBounds { Left = 100, Top = 100, Width = 3000, Height = 2000 };

        var result = WindowPlacement.Clamp(rect, Screen);

        Assert.Equal(Screen.Left, result.Left);
        Assert.Equal(Screen.Top, result.Top);
    }
}

public class WindowPlacementStoreTests
{
    static readonly WindowBounds Default = new WindowBounds { Left = 100, Top = 100, Width = 420, Height = 520 };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "window-placement.json");
        Assert.Equal(Default, new WindowPlacementStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new WindowPlacementStore(path).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "window-placement.json");
        var store = new WindowPlacementStore(path);
        var bounds = new WindowBounds { Left = 42, Top = 7, Width = 600, Height = 300 };

        store.Save(bounds);

        Assert.Equal(bounds, store.Load());
    }
}
