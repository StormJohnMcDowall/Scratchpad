using System.Collections.Generic;
using System.Linq;
using Scratchpad.Core;
using Xunit;

public class CodeHighlighterTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    [InlineData("C#")]
    [InlineData("CSharp")]
    [InlineData("json")]
    [InlineData("js")]
    [InlineData("JavaScript")]
    [InlineData("html")]
    [InlineData("htm")]
    [InlineData("xml")]
    [InlineData("css")]
    [InlineData("c# {.x}")]
    [InlineData("  json  ")]
    public void IsSupported_KnownTags(string tag)
    {
        Assert.True(CodeHighlighter.IsSupported(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("python")]
    [InlineData("text")]
    [InlineData("csharpx")]
    public void IsSupported_OtherTags(string tag)
    {
        Assert.False(CodeHighlighter.IsSupported(tag));
    }

    [Fact]
    public void Tokenize_Unsupported_ReturnsNull()
    {
        Assert.Null(CodeHighlighter.Tokenize("x = 1", "python"));
    }

    [Fact]
    public void Tokenize_Empty_ReturnsEmpty()
    {
        Assert.Empty(CodeHighlighter.Tokenize("", "csharp"));
    }

    [Fact]
    public void Tokenize_CSharp_ColoursKeywordsNumbersCommentsStrings()
    {
        var code = "for (int i = 0; i < 10; i++)\n{\n  // Code to execute.\n  var s = \"hi\";\n}";

        var tokens = CodeHighlighter.Tokenize(code, "csharp");

        Assert.Equal(code, Joined(tokens));
        Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "for"));
        Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "int"));
        Assert.Equal(CodeTokenKind.Number, KindOf(tokens, "10"));
        Assert.Equal(CodeTokenKind.Comment, KindOf(tokens, "// Code to execute."));
        Assert.Equal(CodeTokenKind.String, KindOf(tokens, "\"hi\""));
    }

    [Fact]
    public void Tokenize_Json_KeysAndValues()
    {
        var code = "{\"a\": 1, \"b\": [true, \"x\"]}";

        var tokens = CodeHighlighter.Tokenize(code, "json");

        Assert.Equal(code, Joined(tokens));
        Assert.Equal(CodeTokenKind.JsonKey, KindOf(tokens, "\"a\""));
        Assert.Equal(CodeTokenKind.Number, KindOf(tokens, "1"));
        Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "true"));
        Assert.Equal(CodeTokenKind.String, KindOf(tokens, "\"x\""));
    }

    [Fact]
    public void Tokenize_Html_TagsAttributesEntities()
    {
        var code = "<div class=\"x\">a &amp; b</div>";

        var tokens = CodeHighlighter.Tokenize(code, "html");

        Assert.Equal(code, Joined(tokens));
        Assert.Equal(CodeTokenKind.MarkupElement, KindOf(tokens, "div"));
        Assert.Equal(CodeTokenKind.MarkupAttribute, KindOf(tokens, "class"));
        Assert.Equal(CodeTokenKind.MarkupAttributeValue, KindOf(tokens, "\"x\""));
        Assert.Equal(CodeTokenKind.MarkupEntity, KindOf(tokens, "&amp;"));
    }

    [Fact]
    public void Tokenize_MultiLineComment_StaysOneCommentTokenWithItsNewlines()
    {
        var code = "/* one\ntwo */\nint x;";

        var tokens = CodeHighlighter.Tokenize(code, "js");

        Assert.Equal(code, Joined(tokens));
        Assert.Equal(CodeTokenKind.Comment, KindOf(tokens, "/* one\ntwo */"));
    }

    [Theory]
    [InlineData("\"abc", "csharp")]
    [InlineData("<div", "html")]
    [InlineData("{", "json")]
    [InlineData("/* never closed", "css")]
    [InlineData("<a><b></a>", "xml")]
    public void Tokenize_JunkInput_RoundTripsWithoutThrowing(string code, string tag)
    {
        var tokens = CodeHighlighter.Tokenize(code, tag);

        Assert.Equal(code, Joined(tokens));
        Assert.DoesNotContain(tokens, t => t.Text.Length == 0);
    }

    [Fact]
    public void Tokenize_AdjacentSameKindTokensAreMerged()
    {
        var tokens = CodeHighlighter.Tokenize("a b c", "csharp");

        for (var i = 1; i < tokens.Count; i++)
        {
            Assert.NotEqual(tokens[i - 1].Kind, tokens[i].Kind);
        }
    }

    private static string Joined(IReadOnlyList<CodeToken> tokens)
    {
        return string.Concat(tokens.Select(t => t.Text));
    }

    private static CodeTokenKind KindOf(IReadOnlyList<CodeToken> tokens, string text)
    {
        return tokens.First(t => t.Text == text).Kind;
    }
}
