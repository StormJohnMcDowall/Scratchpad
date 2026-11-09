using Scratchpad.Core;
using Xunit;

public class NoteLinksTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("HTTP://x.org/a?b=1")]
    [InlineData("mailto:a@b.com")]
    public void IsOpenable_WebAndMail_True(string url)
    {
        Assert.True(NoteLinks.IsOpenable(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("foo")]
    [InlineData("/relative")]
    [InlineData("file:///c:/x.exe")]
    [InlineData(@"C:\x.exe")]
    [InlineData(@"\\server\share")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x")]
    public void IsOpenable_Other_False(string url)
    {
        Assert.False(NoteLinks.IsOpenable(url));
    }

    private const string LinkText = "see [docs](https://d.io) now";

    [Theory]
    [InlineData(4)]   // [
    [InlineData(5)]   // d of docs
    [InlineData(11)]  // h of https
    [InlineData(23)]  // )
    public void UrlAt_InsideLink_ReturnsUrl(int index)
    {
        Assert.Equal("https://d.io", NoteLinks.UrlAt(LinkText, index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public void UrlAt_OutsideLink_ReturnsNull(int index)
    {
        Assert.Null(NoteLinks.UrlAt(LinkText, index));
    }

    [Theory]
    [InlineData("<https://a.b>", "https://a.b")]
    [InlineData("<me@x.com>", "mailto:me@x.com")]
    public void UrlAt_Autolink_ReturnsUrl(string markdown, string expected)
    {
        Assert.Equal(expected, NoteLinks.UrlAt(markdown, 3));
    }

    [Fact]
    public void UrlAt_Image_ReturnsNull()
    {
        Assert.Null(NoteLinks.UrlAt("![pic](https://a/p.png)", 3));
    }

    [Fact]
    public void UrlAt_LinkInsideBold_ReturnsUrl()
    {
        Assert.Equal("https://b.c", NoteLinks.UrlAt("**[b](https://b.c)**", 3));
    }

    [Fact]
    public void UrlAt_ReferenceLink_ReturnsResolvedUrl()
    {
        Assert.Equal("https://r.s", NoteLinks.UrlAt("[x][r]\n\n[r]: https://r.s", 1));
    }
}
