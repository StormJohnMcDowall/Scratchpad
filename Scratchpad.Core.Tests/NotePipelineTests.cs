using Markdig;
using Scratchpad.Core;
using Xunit;

public class NotePipelineTests
{
    [Fact]
    public void DoubleTilde_IsStrikethrough()
    {
        Assert.Equal("<p>a <del>gone</del> b</p>", Markdown.ToHtml("a ~~gone~~ b", NotePipeline.Create()).Trim());
    }

    // Only strikethrough: the other emphasis extras would turn "~5 mins" style text into
    // subscript, and ^ ++ == into superscript, underline and highlight.
    [Theory]
    [InlineData("H~2~O")]
    [InlineData("2^10^")]
    [InlineData("++ins++")]
    [InlineData("==mark==")]
    public void OtherEmphasisExtras_StayPlainText(string markdown)
    {
        Assert.Equal($"<p>{markdown}</p>", Markdown.ToHtml(markdown, NotePipeline.Create()).Trim());
    }

    [Fact]
    public void TaskLists_StillWork()
    {
        Assert.Contains("type=\"checkbox\"", Markdown.ToHtml("- [ ] todo", NotePipeline.Create()));
    }
}
