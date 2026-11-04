using Scratchpad.Core;
using Xunit;

public class PreviewSourceMapTests
{
    [Fact]
    public void Build_EmptyText_ReturnsNoBlocks()
    {
        Assert.Empty(PreviewSourceMap.Build(""));
        Assert.Empty(PreviewSourceMap.Build(null));
    }

    [Fact]
    public void Build_ParagraphsHeadingsCodeAndRule_OneBlockEach()
    {
        var markdown = "# Title\n\nPara one\n\n```\ncode\n```\n\n---\n";

        var blocks = PreviewSourceMap.Build(markdown);

        Assert.Equal(4, blocks.Count);
        Assert.Equal("# Title", markdown.Substring(blocks[0].Start, blocks[0].End - blocks[0].Start));
        Assert.Equal(markdown.IndexOf("Para one"), blocks[1].Start);
        Assert.Equal(markdown.IndexOf("```"), blocks[2].Start);
        Assert.Equal(markdown.IndexOf("---"), blocks[3].Start);
    }

    [Fact]
    public void Build_ListItemsAndQuotes_YieldTheirInnerParagraphs()
    {
        var markdown = "- [ ] alpha\n- beta\n\n> quoted\n";

        var blocks = PreviewSourceMap.Build(markdown);

        Assert.Equal(3, blocks.Count);
        Assert.Equal(markdown.IndexOf("[ ] alpha"), blocks[0].Start);
        Assert.Equal(markdown.IndexOf("beta"), blocks[1].Start);
        Assert.Equal(markdown.IndexOf("quoted"), blocks[2].Start);
    }

    [Fact]
    public void Build_LinkReferenceDefinitionsAndHtml_ProduceNoBlock()
    {
        var markdown = "Para\n\n[id]: http://x\n\n<div>hi</div>\n\nAfter\n";

        var blocks = PreviewSourceMap.Build(markdown);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(markdown.IndexOf("Para"), blocks[0].Start);
        Assert.Equal(markdown.IndexOf("After"), blocks[1].Start);
    }

    [Fact]
    public void Build_CrLfText_OffsetsIndexTheOriginalString()
    {
        var markdown = "One\r\n\r\nTwo\r\n";

        var blocks = PreviewSourceMap.Build(markdown);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(markdown.IndexOf("Two"), blocks[1].Start);
        Assert.Equal("Two", markdown.Substring(blocks[1].Start, blocks[1].End - blocks[1].Start));
    }

    [Fact]
    public void BlockIndexAt_OffsetInsideBlock_ReturnsThatBlock()
    {
        var markdown = "One\n\nTwo words\n";
        var blocks = PreviewSourceMap.Build(markdown);

        var index = PreviewSourceMap.BlockIndexAt(blocks, markdown.IndexOf("words"));

        Assert.Equal(1, index);
    }

    [Fact]
    public void BlockIndexAt_OffsetOnBlankLineBetweenBlocks_ReturnsNextBlock()
    {
        var markdown = "One\n\nTwo\n";
        var blocks = PreviewSourceMap.Build(markdown);

        var index = PreviewSourceMap.BlockIndexAt(blocks, 4);

        Assert.Equal(1, index);
    }

    [Fact]
    public void BlockIndexAt_OffsetPastEnd_ReturnsLastBlock()
    {
        var markdown = "One\n\nTwo\n\n\n\n";
        var blocks = PreviewSourceMap.Build(markdown);

        var index = PreviewSourceMap.BlockIndexAt(blocks, markdown.Length);

        Assert.Equal(1, index);
    }

    [Fact]
    public void BlockIndexAt_NoBlocks_ReturnsMinusOne()
    {
        var index = PreviewSourceMap.BlockIndexAt(PreviewSourceMap.Build(""), 0);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void LocateInBlock_FindsRunTextAndAddsOffset()
    {
        var markdown = "Some **bold** words\n";
        var block = PreviewSourceMap.Build(markdown)[0];

        var offset = PreviewSourceMap.LocateInBlock(markdown, block, "words", 2);

        Assert.Equal(markdown.IndexOf("words") + 2, offset);
    }

    [Fact]
    public void LocateInBlock_TextNotInBlock_ReturnsBlockStart()
    {
        var markdown = "Intro\n\nSome words\n";
        var block = PreviewSourceMap.Build(markdown)[1];

        Assert.Equal(block.Start, PreviewSourceMap.LocateInBlock(markdown, block, "zzz", 1));
        Assert.Equal(block.Start, PreviewSourceMap.LocateInBlock(markdown, block, "", 0));
        Assert.Equal(block.Start, PreviewSourceMap.LocateInBlock(markdown, block, null, 0));
    }

    [Fact]
    public void LocateInBlock_SearchesOnlyWithinTheBlock()
    {
        var markdown = "alpha\n\nbeta alpha\n";
        var block = PreviewSourceMap.Build(markdown)[1];

        var offset = PreviewSourceMap.LocateInBlock(markdown, block, "alpha", 0);

        Assert.Equal(markdown.LastIndexOf("alpha"), offset);
    }

    [Fact]
    public void Build_FencedCode_CarriesItsLanguage()
    {
        var blocks = PreviewSourceMap.Build("Para\n\n```csharp\nint x;\n```\n\n```\nplain\n```\n\n    indented\n");

        Assert.Equal(4, blocks.Count);
        Assert.Null(blocks[0].CodeLanguage);
        Assert.Equal("csharp", blocks[1].CodeLanguage);
        Assert.Null(blocks[2].CodeLanguage);
        Assert.Null(blocks[3].CodeLanguage);
    }

    [Fact]
    public void Build_FencedCodeInsideListAndQuote_CarriesItsLanguage()
    {
        var blocks = PreviewSourceMap.Build("- item\n\n  ```json\n  {}\n  ```\n\n> ```js\n> x\n> ```\n\nAfter\n");

        Assert.Equal(4, blocks.Count);
        Assert.Equal("json", blocks[1].CodeLanguage);
        Assert.Equal("js", blocks[2].CodeLanguage);
        Assert.Null(blocks[3].CodeLanguage);
    }
}
