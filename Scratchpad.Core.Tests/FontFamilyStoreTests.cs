using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class FontFamilyStoreTests
{
    static readonly FontFamilySettings Default = new FontFamilySettings
    {
        EditorFontFamily = FontFamilyStore.DefaultEditorFontFamily,
        PreviewNormalFontFamily = null,
        PreviewCodeFontFamily = null
    };

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        Assert.Equal(Default, new FontFamilyStore(path).Load());
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");
        Assert.Equal(Default, new FontFamilyStore(path).Load());
    }

    [Fact]
    public void Load_NormalizesEmptyEditorFontFamilyToDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontFamily\":\"\",\"PreviewNormalFontFamily\":null,\"PreviewCodeFontFamily\":null}");

        Assert.Equal(FontFamilyStore.DefaultEditorFontFamily, new FontFamilyStore(path).Load().EditorFontFamily);
    }

    [Fact]
    public void Load_NormalizesWhitespacePreviewFontFamilyToNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"EditorFontFamily\":\"Consolas\",\"PreviewNormalFontFamily\":\"   \",\"PreviewCodeFontFamily\":null}");

        Assert.Null(new FontFamilyStore(path).Load().PreviewNormalFontFamily);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsIndependentValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        var store = new FontFamilyStore(path);
        var settings = new FontFamilySettings
        {
            EditorFontFamily = "Cascadia Mono",
            PreviewNormalFontFamily = "Georgia",
            PreviewCodeFontFamily = "Courier New"
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNullPreviewFields()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "font-family.json");
        var store = new FontFamilyStore(path);

        store.Save(Default);

        Assert.Equal(Default, store.Load());
    }
}
