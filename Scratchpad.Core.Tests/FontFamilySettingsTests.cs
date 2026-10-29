using Scratchpad.Core;
using Xunit;

public class FontFamilySettingsTests
{
    [Fact]
    public void IsDefault_TrueForDefaultSettings()
    {
        var settings = new FontFamilySettings
        {
            EditorFontFamily = FontFamilyStore.DefaultEditorFontFamily,
            PreviewNormalFontFamily = null,
            PreviewCodeFontFamily = null
        };

        Assert.True(settings.IsDefault());
    }

    [Fact]
    public void IsDefault_FalseWhenEditorFontDiffers()
    {
        var settings = new FontFamilySettings
        {
            EditorFontFamily = "Cascadia Mono",
            PreviewNormalFontFamily = null,
            PreviewCodeFontFamily = null
        };

        Assert.False(settings.IsDefault());
    }

    [Fact]
    public void IsDefault_FalseWhenPreviewNormalFontIsSet()
    {
        var settings = new FontFamilySettings
        {
            EditorFontFamily = FontFamilyStore.DefaultEditorFontFamily,
            PreviewNormalFontFamily = "Georgia",
            PreviewCodeFontFamily = null
        };

        Assert.False(settings.IsDefault());
    }

    [Fact]
    public void IsDefault_FalseWhenPreviewCodeFontIsSet()
    {
        var settings = new FontFamilySettings
        {
            EditorFontFamily = FontFamilyStore.DefaultEditorFontFamily,
            PreviewNormalFontFamily = null,
            PreviewCodeFontFamily = "Courier New"
        };

        Assert.False(settings.IsDefault());
    }
}
