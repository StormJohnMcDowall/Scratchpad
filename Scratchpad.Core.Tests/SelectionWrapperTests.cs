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
    public void Wrap_SelectionAndWrapChar_WrapsAndKeepsTextSelected(string c)
    {
        var text = "say hello now";

        var edit = SelectionWrapper.Wrap(text, 4, 5, c);

        Assert.Equal($"say {c}hello{c} now", Apply(text, edit));
        Assert.Equal("hello", Selected(text, edit));
    }

    // Strikethrough is ~~double~~, and that's all a single ~ wrap could be for.
    [Fact]
    public void Wrap_Tilde_WrapsInDoubleTildes()
    {
        var text = "say hello now";

        var edit = SelectionWrapper.Wrap(text, 4, 5, "~");

        Assert.Equal("say ~~hello~~ now", Apply(text, edit));
        Assert.Equal("hello", Selected(text, edit));
    }

    // A second ~ removes the strikethrough rather than making ~~~~, which at a line start
    // opens a code fence and swallows the rest of the note.
    [Theory]
    [InlineData("say hello now", 4, 5)]
    [InlineData("hello\nmore", 0, 5)]
    public void Wrap_TildeTwice_TogglesStrikethroughOff(string text, int start, int length)
    {
        var first = SelectionWrapper.Wrap(text, start, length, "~");
        var once = Apply(text, first);

        var second = SelectionWrapper.Wrap(once, first.SelectionStart, first.SelectionLength, "~");

        Assert.Equal(text, Apply(once, second));
        Assert.Equal("hello", Selected(once, second));
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

    // Double-click and Ctrl+Shift+Right in a WPF TextBox select the word plus the space after
    // it; "*hello *" isn't emphasis.
    [Theory]
    [InlineData(4, 6)]  // "hello "
    [InlineData(3, 6)]  // " hello"
    [InlineData(3, 7)]  // " hello "
    public void Wrap_SurroundingSpaces_StayOutside(int start, int length)
    {
        var text = "say hello now";

        var edit = SelectionWrapper.Wrap(text, start, length, "*");

        Assert.Equal("say *hello* now", Apply(text, edit));
        Assert.Equal("hello", Selected(text, edit));
    }

    [Fact]
    public void Wrap_TrailingBlankLine_StaysOutside()
    {
        var text = "abc\n\ndef";

        var edit = SelectionWrapper.Wrap(text, 0, 5, "*");

        Assert.Equal("*abc*\n\ndef", Apply(text, edit));
        Assert.Equal("abc", Selected(text, edit));
    }

    [Theory]
    [InlineData("a   b", 1, 3)]
    [InlineData("a \t\r\n b", 1, 5)]
    public void Wrap_OnlyWhitespace_ReturnsNull(string text, int start, int length)
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
