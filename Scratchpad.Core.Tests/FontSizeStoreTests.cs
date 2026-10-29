using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class FontSizeStoreTests
{
    static readonly FontSizeSettings Default = new FontSizeSettings
    {
        EditorFontSize = FontSizing.Default,
        PreviewFontSize = FontSizing.Default
    };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        Assert.Equal(Default, new FontSizeStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new FontSizeStore(path).Load());
    }

    [Fact]
    public void Load_ClampsOutOfRangeStoredValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontSize\":9999,\"PreviewFontSize\":-5}");

        var result = new FontSizeStore(path).Load();

        Assert.Equal(FontSizing.Max, result.EditorFontSize);
        Assert.Equal(FontSizing.Min, result.PreviewFontSize);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsIndependentValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        var store = new FontSizeStore(path);
        var settings = new FontSizeSettings { EditorFontSize = 20, PreviewFontSize = 10 };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Save_WithNaNValues_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-size.json");
        var store = new FontSizeStore(path);
        var settings = new FontSizeSettings { EditorFontSize = double.NaN, PreviewFontSize = double.NaN };

        var ex = Record.Exception(() => store.Save(settings));

        Assert.Null(ex);
    }
}
