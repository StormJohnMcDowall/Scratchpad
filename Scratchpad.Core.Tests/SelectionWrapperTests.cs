using Scratchpad.Core;
using Xunit;

public class SelectionWrapperTests
{
    private static string Apply(string text, IndentEdit edit)
    {
        return text.Substring(0, edit.ReplaceStart) + edit.Replacement + text.Substring(edit.ReplaceStart + edit.ReplaceLength);
    }

    private static string Selected(string text, IndentEdit edit)
    {
        return Apply(text, edit).Substring(edit.SelectionStart, edit.SelectionLength);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("_")]
    [InlineData("`")]
    [InlineData("~")]
    public void Wrap_SelectionAndWrapChar_WrapsAndKeepsTextSelected(string c)
    {
        var text = "say hello now";

        var edit = SelectionWrapper.Wrap(text, 4, 5, c);

        Assert.Equal($"say {c}hello{c} now", Apply(text, edit));
        Assert.Equal("hello", Selected(text, edit));
    }

    [Fact]
    public void Wrap_TwiceWithStar_MakesBold()
    {
        var text = "hello";
        var first = SelectionWrapper.Wrap(text, 0, 5, "*");
        var once = Apply(text, first);

        var second = SelectionWrapper.Wrap(once, first.SelectionStart, first.SelectionLength, "*");

        Assert.Equal("**hello**", Apply(once, second));
        Assert.Equal("hello", Selected(once, second));
    }

    [Fact]
    public void Wrap_NoSelection_ReturnsNull()
    {
        Assert.Null(SelectionWrapper.Wrap("abc", 1, 0, "*"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("(")]
    [InlineData("**")]
    [InlineData("")]
    [InlineData(null)]
    public void Wrap_NotASingleWrapChar_ReturnsNull(string typed)
    {
        Assert.Null(SelectionWrapper.Wrap("abc", 0, 3, typed));
    }

    [Theory]
    [InlineData("abc\ndef", 0, 4)]
    [InlineData("abc\r\ndef", 0, 5)]
    public void Wrap_TrailingLineBreak_StaysOutside(string text, int start, int length)
    {
        var edit = SelectionWrapper.Wrap(text, start, length, "*");

        Assert.Equal("*abc*\ndef", Apply(text, edit).Replace("\r\n", "\n"));
        Assert.Equal("abc", Selected(text, edit));
    }

    [Theory]
    [InlineData("abc\ndef", 3, 1)]
    [InlineData("abc\r\ndef", 3, 2)]
    public void Wrap_OnlyALineBreak_ReturnsNull(string text, int start, int length)
    {
        Assert.Null(SelectionWrapper.Wrap(text, start, length, "*"));
    }

    [Fact]
    public void Wrap_MultiLineSelection_WrapsWhole()
    {
        var text = "ab\ncd";

        var edit = SelectionWrapper.Wrap(text, 1, 3, "_");

        Assert.Equal("a_b\nc_d", Apply(text, edit));
        Assert.Equal("b\nc", Selected(text, edit));
    }
}
