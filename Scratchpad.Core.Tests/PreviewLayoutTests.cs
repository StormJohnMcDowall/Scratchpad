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
}
