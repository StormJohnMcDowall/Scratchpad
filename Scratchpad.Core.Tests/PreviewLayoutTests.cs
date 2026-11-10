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
    public void TaskListLeftPadding_ScalesWithFontSize() =>
        Assert.Equal(8, PreviewLayout.TaskListLeftPadding(16), 6);
}
