using Scratchpad.Core;
using Xunit;

public class TaskListTogglerTests
{
    [Fact]
    public void Toggle_ChecksAnUncheckedItem()
    {
        var markdown = "- [ ] alpha\n- [x] beta\n";

        var result = TaskListToggler.Toggle(markdown, 0);

        Assert.Equal("- [x] alpha\n- [x] beta\n", result);
    }

    [Fact]
    public void Toggle_UnchecksACheckedItem()
    {
        var markdown = "- [ ] alpha\n- [x] beta\n";

        var result = TaskListToggler.Toggle(markdown, 1);

        Assert.Equal("- [ ] alpha\n- [ ] beta\n", result);
    }

    [Fact]
    public void Toggle_TreatsUppercaseXAsChecked()
    {
        var markdown = "- [X] alpha\n";

        var result = TaskListToggler.Toggle(markdown, 0);

        Assert.Equal("- [ ] alpha\n", result);
    }

    [Fact]
    public void Toggle_TwiceRestoresTheOriginalText()
    {
        var markdown = "- [ ] alpha\n- [x] beta\n- [ ] gamma\n";

        var result = TaskListToggler.Toggle(TaskListToggler.Toggle(markdown, 0), 0);

        Assert.Equal(markdown, result);
    }

    [Fact]
    public void Toggle_SkipsRegularListItemsWhenCountingTaskIndex()
    {
        var markdown = "1. [ ] first\n2. not a task\n3. [x] third\n";

        var result = TaskListToggler.Toggle(markdown, 1);

        Assert.Equal("1. [ ] first\n2. not a task\n3. [ ] third\n", result);
    }

    [Fact]
    public void Toggle_HandlesNestedLists_InDocumentOrder()
    {
        var markdown = "- [ ] parent\n  - [ ] child one\n  - [x] child two\n- [ ] sibling\n";

        var result = TaskListToggler.Toggle(markdown, 1);

        Assert.Equal("- [ ] parent\n  - [x] child one\n  - [x] child two\n- [ ] sibling\n", result);
    }

    [Fact]
    public void Toggle_WithOutOfRangeIndex_ReturnsTextUnchanged()
    {
        var markdown = "- [ ] alpha\n";

        var result = TaskListToggler.Toggle(markdown, 5);

        Assert.Equal(markdown, result);
    }

    [Fact]
    public void Toggle_WithNegativeIndex_ReturnsTextUnchanged()
    {
        var markdown = "- [ ] alpha\n";

        var result = TaskListToggler.Toggle(markdown, -1);

        Assert.Equal(markdown, result);
    }
}
