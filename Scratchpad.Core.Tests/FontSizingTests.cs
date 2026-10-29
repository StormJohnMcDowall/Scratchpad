using Scratchpad.Core;
using Xunit;

public class FontSizingTests
{
    [Fact]
    public void Increase_AddsOneStep() =>
        Assert.Equal(16, FontSizing.Increase(14));

    [Fact]
    public void Increase_ClampsAtMax() =>
        Assert.Equal(FontSizing.Max, FontSizing.Increase(FontSizing.Max - 1));

    [Fact]
    public void Decrease_SubtractsOneStep() =>
        Assert.Equal(12, FontSizing.Decrease(14));

    [Fact]
    public void Decrease_ClampsAtMin() =>
        Assert.Equal(FontSizing.Min, FontSizing.Decrease(FontSizing.Min + 1));

    [Theory]
    [InlineData(9999, 36)]
    [InlineData(-5, 8)]
    [InlineData(0, 8)]
    [InlineData(20, 20)]
    public void Clamp_KeepsValueWithinRange(double value, double expected) =>
        Assert.Equal(expected, FontSizing.Clamp(value));
}
