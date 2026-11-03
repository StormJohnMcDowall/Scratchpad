using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class DateFormatStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "date-format.json");

        Assert.Equal(DateFormatStore.Defaults, new DateFormatStore(path).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "date-format.json");

        new DateFormatStore(path).Save(new DateFormatSettings { Format = "dd/MM/yyyy" });

        Assert.Equal("dd/MM/yyyy", new DateFormatStore(path).Load().Format);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "not json");

        Assert.Equal(DateFormatStore.Defaults, new DateFormatStore(path).Load());
    }

    [Fact]
    public void Load_BlankFormat_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "date-format.json");
        new DateFormatStore(path).Save(new DateFormatSettings { Format = "  " });

        Assert.Equal(DateFormatStore.Defaults, new DateFormatStore(path).Load());
    }

    [Fact]
    public void Defaults_FormatIsDddDMmm()
    {
        Assert.Equal("ddd d MMM", DateFormatStore.Defaults.Format);
    }
}
