using System;
using System.IO;
using Scratchpad.Core;
using Xunit;

public class BackupStoreTests
{
    private static readonly DateTime T0 = new DateTime(2026, 11, 4, 10, 0, 0);

    private static string NewFolder() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "backups");

    private static int CopyCount(string folder) => Directory.GetFiles(folder).Length;

    [Fact]
    public void TryWriteRegularIfDue_FirstCopy_WritesNamedFileWithText()
    {
        var folder = NewFolder();

        Assert.True(new BackupStore(folder).TryWriteRegularIfDue("my note", T0));

        Assert.Equal("my note", File.ReadAllText(Path.Combine(folder, "scratchpad 2026-11-04 100000.md")));
    }

    [Fact]
    public void TryWriteRegularIfDue_WithinTheHour_WritesNothing()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("one", T0);

        Assert.False(store.TryWriteRegularIfDue("two", T0.AddMinutes(30)));
        Assert.Equal(1, CopyCount(folder));
    }

    [Fact]
    public void TryWriteRegularIfDue_AfterTheHour_SameText_WritesNothing()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("same", T0);

        Assert.False(store.TryWriteRegularIfDue("same", T0.AddHours(2)));
    }

    [Fact]
    public void TryWriteRegularIfDue_AfterTheHour_NewText_Writes()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("one", T0);

        Assert.True(store.TryWriteRegularIfDue("two", T0.AddHours(1)));
        Assert.Equal(2, CopyCount(folder));
    }

    // A restart mustn't forget the last copy and write another straight away.
    [Fact]
    public void TryWriteRegularIfDue_ReadsNewestRegularFromExistingFolder()
    {
        var folder = NewFolder();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, BackupRules.FileName(T0, BackupKind.Regular)), "earlier");

        Assert.False(new BackupStore(folder).TryWriteRegularIfDue("later", T0.AddMinutes(10)));
    }

    [Fact]
    public void TryWriteBeforeBigDelete_IgnoresHourAndDoesNotResetIt()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("one", T0);

        Assert.True(store.TryWriteBeforeBigDelete("old note", T0.AddMinutes(5)));
        Assert.True(store.TryWriteRegularIfDue("two", T0.AddMinutes(61)));
    }

    // The first copy in a second holds the real old note; a second big delete mustn't replace it.
    [Fact]
    public void TryWriteBeforeBigDelete_SameSecond_KeepsFirst()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);

        Assert.True(store.TryWriteBeforeBigDelete("old1", T0));
        Assert.False(store.TryWriteBeforeBigDelete("old2", T0));

        Assert.Equal("old1", File.ReadAllText(Path.Combine(folder, BackupRules.FileName(T0, BackupKind.BeforeBigDelete))));
    }

    [Fact]
    public void TryWriteOnExit_DifferentText_WritesWithinTheHour()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("one", T0);

        Assert.True(store.TryWriteOnExit("two", T0.AddMinutes(5)));
    }

    [Fact]
    public void TryWriteOnExit_SameText_WritesNothing()
    {
        var folder = NewFolder();
        var store = new BackupStore(folder);
        store.TryWriteRegularIfDue("one", T0);

        Assert.False(store.TryWriteOnExit("one", T0.AddMinutes(5)));
    }

    [Fact]
    public void Write_PrunesOldCopiesButNotForeignFiles()
    {
        var folder = NewFolder();
        Directory.CreateDirectory(folder);
        var oldCopy = Path.Combine(folder, BackupRules.FileName(T0.AddDays(-40), BackupKind.Regular));
        var foreign = Path.Combine(folder, "notes.txt");
        File.WriteAllText(oldCopy, "ancient");
        File.WriteAllText(foreign, "mine");

        new BackupStore(folder).TryWriteRegularIfDue("now", T0);

        Assert.False(File.Exists(oldCopy));
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public void Write_FolderIsAFile_ReturnsFalseWithoutThrowing()
    {
        var folder = NewFolder();
        Directory.CreateDirectory(Path.GetDirectoryName(folder));
        File.WriteAllText(folder, "in the way");

        Assert.False(new BackupStore(folder).TryWriteRegularIfDue("note", T0));
    }
}
