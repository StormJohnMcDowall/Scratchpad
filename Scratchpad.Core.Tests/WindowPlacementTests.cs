using System;
using System.Collections.Generic;
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

    [Fact]
    public void Clamp_WithMultipleScreens_MovesWindowOntoAScreenWhenOutsideAllOfThem()
    {
        // A docking-station layout where the virtual screen's bounding box is not itself a screen:
        // primary bottom-left, secondary up and to the right, leaving an empty region top-left.
        var primary = new WindowBounds { Left = 0, Top = 0, Width = 1920, Height = 1080 };
        var secondary = new WindowBounds { Left = 1920, Top = -1080, Width = 1920, Height = 1080 };
        var screens = new List<WindowBounds> { primary, secondary };
        var requested = new WindowBounds { Left = 0, Top = -1080, Width = 420, Height = 520 };

        var result = WindowPlacement.Clamp(requested, screens);

        bool OnScreen(WindowBounds screen) =>
            result.Left >= screen.Left && result.Left + result.Width <= screen.Left + screen.Width
            && result.Top >= screen.Top && result.Top + result.Height <= screen.Top + screen.Height;

        Assert.True(OnScreen(primary) || OnScreen(secondary));
    }

    [Fact]
    public void Clamp_WithMultipleScreens_LeavesWindowUnchangedWhenAlreadyOnASecondaryScreen()
    {
        var primary = new WindowBounds { Left = 0, Top = 0, Width = 1920, Height = 1080 };
        var secondary = new WindowBounds { Left = 1920, Top = 0, Width = 1920, Height = 1080 };
        var screens = new List<WindowBounds> { primary, secondary };
        var requested = new WindowBounds { Left = 2000, Top = 100, Width = 420, Height = 520 };

        var result = WindowPlacement.Clamp(requested, screens);

        Assert.Equal(requested, result);
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

    [Fact]
    public void Save_WithNaNValues_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "window-placement.json");
        var store = new WindowPlacementStore(path);
        var bounds = new WindowBounds { Left = double.NaN, Top = double.NaN, Width = double.NaN, Height = double.NaN };

        var ex = Record.Exception(() => store.Save(bounds));

        Assert.Null(ex);
    }
}
