using System;
using System.Globalization;
using Scratchpad.Core;
using Xunit;

public class DateStampTests
{
    private static readonly DateTime Tuesday = new DateTime(2026, 11, 3);
    private static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");

    [Fact]
    public void Format_DefaultFormat_GivesShortDayDayAndMonth()
    {
        Assert.Equal("Tue 3 Nov", DateStamp.Format(DateStamp.DefaultFormat, Tuesday, EnGb));
    }

    [Fact]
    public void Format_CustomFormat_IsUsed()
    {
        Assert.Equal("2026-11-03", DateStamp.Format("yyyy-MM-dd", Tuesday, EnGb));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Format_NullOrWhitespace_FallsBackToDefault(string format)
    {
        Assert.Equal("Tue 3 Nov", DateStamp.Format(format, Tuesday, EnGb));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("ddd \\")]
    public void Format_InvalidFormat_FallsBackToDefault(string format)
    {
        Assert.Equal("Tue 3 Nov", DateStamp.Format(format, Tuesday, EnGb));
    }
}
