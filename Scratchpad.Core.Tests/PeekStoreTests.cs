using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class PeekStoreTests
{
    [Fact]
    public void Defaults_SecondsIsThree()
    {
        Assert.Equal(3, PeekStore.Defaults.Seconds);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "peek.json");

        Assert.Equal(PeekStore.Defaults, new PeekStore(path).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");

        Assert.Equal(PeekStore.Defaults, new PeekStore(path).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "peek.json");

        new PeekStore(path).Save(new PeekSettings { Seconds = 10 });

        Assert.Equal(10, new PeekStore(path).Load().Seconds);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 30)]
    public void Load_OutOfRangeFile_IsClamped(int saved, int expected)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "peek.json");
        new PeekStore(path).Save(new PeekSettings { Seconds = saved });

        Assert.Equal(expected, new PeekStore(path).Load().Seconds);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData(" 7 ", 7)]
    [InlineData("1", 1)]
    [InlineData("30", 30)]
    [InlineData("0", 1)]
    [InlineData("-3", 1)]
    [InlineData("45", 30)]
    [InlineData("99999999999", 30)]
    public void ParseSeconds_WholeNumber_IsClampedIntoRange(string text, int expected)
    {
        Assert.Equal(expected, PeekStore.ParseSeconds(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("2.5")]
    public void ParseSeconds_NotAWholeNumber_ReturnsNull(string text)
    {
        Assert.Null(PeekStore.ParseSeconds(text));
    }
}
