using Scratchpad.Core;
using Xunit;

public class LineIndenterTests
{
    private static string Apply(string text, IndentEdit edit)
    {
        return text.Substring(0, edit.ReplaceStart) + edit.Replacement + text.Substring(edit.ReplaceStart + edit.ReplaceLength);
    }

    private static string Selected(string text, IndentEdit edit)
    {
        return Apply(text, edit).Substring(edit.SelectionStart, edit.SelectionLength);
    }

    [Fact]
    public void Indent_SelectionWithinOneLine_ReturnsNull()
    {
        Assert.Null(LineIndenter.Indent("abc\ndef", 1, 1));
    }

    [Fact]
    public void Indent_TwoPartiallySelectedLines_IndentsBoth()
    {
        var text = "abc\ndef\nghi";

        var edit = LineIndenter.Indent(text, 1, 4);

        Assert.Equal("    abc\n    def\nghi", Apply(text, edit));
        Assert.Equal("    abc\n    def", Selected(text, edit));
    }

    [Fact]
    public void Indent_SelectionEndingAtLineStart_ExcludesThatLine()
    {
        var text = "abc\ndef\nghi";

        var edit = LineIndenter.Indent(text, 0, 4);

        Assert.Equal("    abc\ndef\nghi", Apply(text, edit));
        Assert.Equal("    abc\n", Selected(text, edit));
    }

    [Fact]
    public void Indent_SkipsEmptyLines()
    {
        var text = "a\n\nb";

        var edit = LineIndenter.Indent(text, 0, text.Length);

        Assert.Equal("    a\n\n    b", Apply(text, edit));
    }

    [Fact]
    public void Indent_OnlyBlankLines_LeavesTextAloneRatherThanReturningNull()
    {
        var text = "a\n\n\nb";

        var edit = LineIndenter.Indent(text, 2, 2);

        Assert.NotNull(edit);
        Assert.Equal(text, Apply(text, edit));
    }

    [Fact]
    public void Indent_CrLf_SelectionEndsBeforeLineBreak()
    {
        var text = "ab\r\ncd\r\nef";

        var edit = LineIndenter.Indent(text, 0, 5);

        Assert.Equal("    ab\r\n    cd\r\nef", Apply(text, edit));
        Assert.Equal("    ab\r\n    cd", Selected(text, edit));
    }

    [Fact]
    public void Indent_LastLineWithoutNewline_IsIndented()
    {
        var text = "a\nb";

        var edit = LineIndenter.Indent(text, 0, 3);

        Assert.Equal("    a\n    b", Apply(text, edit));
    }

    [Fact]
    public void Outdent_RemovesOneTabOrUpToFourSpaces()
    {
        var text = "\t    x\n  \ty\n      z\nw";

        var edit = LineIndenter.Outdent(text, 0, text.Length);

        Assert.Equal("    x\n\ty\n  z\nw", Apply(text, edit));
    }

    [Fact]
    public void Outdent_NothingToRemove_ReturnsNull()
    {
        Assert.Null(LineIndenter.Outdent("a\nb", 0, 3));
    }

    [Fact]
    public void Outdent_Caret_UnindentsCurrentLineAndKeepsCaretOnSameText()
    {
        var text = "a\n        bc";

        var edit = LineIndenter.Outdent(text, text.IndexOf('c'), 0);

        var result = Apply(text, edit);
        Assert.Equal("a\n    bc", result);
        Assert.Equal(result.IndexOf('c'), edit.SelectionStart);
        Assert.Equal(0, edit.SelectionLength);
    }

    [Fact]
    public void Outdent_CaretInsideRemovedIndent_ClampsToLineStart()
    {
        var text = "a\n      x";

        var edit = LineIndenter.Outdent(text, 4, 0);

        Assert.Equal("a\n  x", Apply(text, edit));
        Assert.Equal(2, edit.SelectionStart);
    }

    [Fact]
    public void Outdent_MultiLine_SelectsWholeTouchedLines()
    {
        var text = "    abc\n    def";

        var edit = LineIndenter.Outdent(text, 5, 6);

        Assert.Equal("abc\ndef", Apply(text, edit));
        Assert.Equal("abc\ndef", Selected(text, edit));
    }
}
