using Scratchpad.Core;
using Xunit;

public class PeekSlideTests
{
    [Theory]
    [InlineData(0.0, true, 100)]
    [InlineData(1.0, true, 900)]
    [InlineData(0.5, true, 300)]   // ease-in: 100 + 800 * 0.25
    [InlineData(0.5, false, 700)]  // ease-out: 100 + 800 * 0.75
    [InlineData(-1.0, true, 100)]  // clamped
    [InlineData(2.0, false, 900)]  // clamped
    public void Interpolate_FromHundredToNineHundred(double progress, bool accelerate, double expected)
    {
        Assert.Equal(expected, PeekSlide.Interpolate(100, 900, progress, accelerate), 6);
    }

    [Fact]
    public void Interpolate_Upwards_EndsAtTarget()
    {
        Assert.Equal(100, PeekSlide.Interpolate(900, 100, 1.0, false), 6);
    }
}
