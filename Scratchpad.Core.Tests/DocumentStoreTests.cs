using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class DocumentStoreTests
{
    [Fact]
    public void DefaultFilePath_IsUnderLocalAppDataScratchpadFolder()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scratchpad", "scratchpad.md");
        Assert.Equal(expected, DocumentStore.DefaultFilePath);
    }

    [Fact]
    public void Load_CreatesEmptyFileWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        var content = store.Load();

        Assert.Equal("", content);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_WritesContentAtomicallyWithNoLeftoverTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        store.Save("hello");

        Assert.Equal("hello", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "scratchpad.md");
        var store = new DocumentStore(path);

        store.Save("first");
        store.Save("second");

        Assert.Equal("second", store.Load());
    }
}
