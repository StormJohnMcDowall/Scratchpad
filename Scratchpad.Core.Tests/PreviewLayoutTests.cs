using Scratchpad.Core;
using Xunit;

public class PreviewLayoutTests
{
    [Fact]
    public void HeadingMargin_ScalesWithFontSize()
    {
        var m = PreviewLayout.HeadingMargin(40, false);
        Assert.Equal(24, m.Top, 6);
        Assert.Equal(6, m.Bottom, 6);
    }

    [Fact]
    public void HeadingMargin_FirstBlock_HasNoTopGap()
    {
        var m = PreviewLayout.HeadingMargin(40, true);
        Assert.Equal(0, m.Top);
        Assert.Equal(6, m.Bottom, 6);
    }

    [Fact]
    public void HeadingMargin_BottomSmallerThanTop()
    {
        var m = PreviewLayout.HeadingMargin(20, false);
        Assert.True(m.Bottom < m.Top);
    }

    [Fact]
    public void IsTaskList_AllItemsTasks_True() =>
        Assert.True(PreviewLayout.IsTaskList(new[] { true, true }));

    [Fact]
    public void IsTaskList_MixedItems_False() =>
        Assert.False(PreviewLayout.IsTaskList(new[] { true, false, true }));

    [Fact]
    public void IsTaskList_ItemWithoutLeadingCheckbox_False() =>
        Assert.False(PreviewLayout.IsTaskList(new[] { false }));

    [Fact]
    public void IsTaskList_NoItems_False() =>
        Assert.False(PreviewLayout.IsTaskList(new bool[0]));

    [Fact]
    public void TaskListLeftIndent_ScalesWithFontSize() =>
        Assert.Equal(14, PreviewLayout.TaskListLeftIndent(10), 6);

    [Fact]
    public void TaskListLeftIndent_SitsWhereTheBulletsWere()
    {
        // Measured on Windows (screenshot 2026-11-10): at the 14px default body size a bullet
        // sits about 20px in from the text edge; the checkbox should start roughly there.
        var indent = PreviewLayout.TaskListLeftIndent(14);
        Assert.InRange(indent, 17, 23);
    }

    [Fact]
    public void HidesMarkers_BulletedTaskList_True() =>
        Assert.True(PreviewLayout.HidesMarkers(false, new[] { true, true }));

    [Fact]
    public void HidesMarkers_NumberedTaskList_KeepsNumbers() =>
        Assert.False(PreviewLayout.HidesMarkers(true, new[] { true, true }));

    [Fact]
    public void HidesMarkers_MixedBulletedList_False() =>
        Assert.False(PreviewLayout.HidesMarkers(false, new[] { true, false }));

    [Fact]
    public void TopMarginUnder_Heading_IsZero() =>
        Assert.Equal(0, PreviewLayout.TopMarginUnder(true, double.NaN));

    [Fact]
    public void TopMarginUnder_Heading_ZeroesASetMarginToo() =>
        Assert.Equal(0, PreviewLayout.TopMarginUnder(true, 12));

    [Fact]
    public void TopMarginUnder_NotHeading_KeepsCurrent() =>
        Assert.Equal(12, PreviewLayout.TopMarginUnder(false, 12));

    [Fact]
    public void TopMarginUnder_NotHeading_KeepsAuto() =>
        Assert.True(double.IsNaN(PreviewLayout.TopMarginUnder(false, double.NaN)));

    [Fact]
    public void Heading1Size_IsAboutTwiceBody() =>
        Assert.Equal(18, PreviewLayout.Heading1Size(10), 6);

    [Fact]
    public void CodeSize_IsSlightlySmallerThanSurroundingText() =>
        Assert.Equal(9, PreviewLayout.CodeSize(10), 6);
}
