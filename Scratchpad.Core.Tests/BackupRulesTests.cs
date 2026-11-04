using System;
using System.Linq;
using Scratchpad.Core;
using Xunit;

public class BackupRulesTests
{
    private static readonly DateTime Now = new DateTime(2026, 11, 4, 12, 0, 0);

    private static string Regular(DateTime time) => BackupRules.FileName(time, BackupKind.Regular);
    private static string BigDelete(DateTime time) => BackupRules.FileName(time, BackupKind.BeforeBigDelete);

    [Theory]
    [InlineData("hello world", "hello world", 0)]
    [InlineData("hello world", "", 11)]
    [InlineData("abcdef", "abXYZef", 2)]
    [InlineData("abc", "abcdef", 0)]
    [InlineData("aaaa", "aa", 2)]
    [InlineData("  line1\n  line2", "line1\nline2", 10)]
    public void RemovedLength_CountsBeforeTextBetweenCommonEnds(string before, string after, int expected)
    {
        Assert.Equal(expected, BackupRules.RemovedLength(before, after));
    }

    [Fact]
    public void IsBigDelete_SelectAllDelete_OnHundredCharNote_IsTrue()
    {
        Assert.True(BackupRules.IsBigDelete(new string('x', 100), ""));
    }

    // Below the 100-character floor, removing half doesn't count.
    [Fact]
    public void IsBigDelete_HalfOfNinetyNineChars_IsFalse()
    {
        Assert.False(BackupRules.IsBigDelete(new string('x', 99), new string('x', 39)));
    }

    [Fact]
    public void IsBigDelete_ExactlyHalf_IsTrue()
    {
        Assert.True(BackupRules.IsBigDelete(new string('x', 200), new string('x', 100)));
    }

    [Fact]
    public void IsBigDelete_JustUnderHalf_IsFalse()
    {
        Assert.False(BackupRules.IsBigDelete(new string('x', 200), new string('x', 101)));
    }

    [Fact]
    public void IsBigDelete_ThousandCharsFromHugeNote_IsTrue()
    {
        Assert.True(BackupRules.IsBigDelete(new string('x', 10000), new string('x', 9000)));
    }

    [Fact]
    public void IsBigDelete_NineHundredNinetyNineFromHugeNote_IsFalse()
    {
        Assert.False(BackupRules.IsBigDelete(new string('x', 10000), new string('x', 9001)));
    }

    // Added text doesn't cancel the removal: pasting over everything counts it all.
    [Fact]
    public void IsBigDelete_PasteOverEverything_IsTrue()
    {
        Assert.True(BackupRules.IsBigDelete(new string('a', 500), new string('b', 600)));
    }

    [Fact]
    public void IsBigDelete_CheckboxToggle_IsFalse()
    {
        var before = new string('x', 150) + "\n- [ ] task\n" + new string('y', 150);
        var after = new string('x', 150) + "\n- [x] task\n" + new string('y', 150);

        Assert.False(BackupRules.IsBigDelete(before, after));
    }

    [Fact]
    public void IsHourlyCopyDue_NoCopyYet_IsTrue()
    {
        Assert.True(BackupRules.IsHourlyCopyDue(null, Now));
    }

    [Fact]
    public void IsHourlyCopyDue_59MinutesLater_IsFalse()
    {
        Assert.False(BackupRules.IsHourlyCopyDue(Now, Now.AddMinutes(59)));
    }

    [Fact]
    public void IsHourlyCopyDue_60MinutesLater_IsTrue()
    {
        Assert.True(BackupRules.IsHourlyCopyDue(Now, Now.AddMinutes(60)));
    }

    // A copy from the "future" (the clock was set back since) mustn't stop hourly copies.
    [Fact]
    public void IsHourlyCopyDue_NewestCopyInTheFuture_IsTrue()
    {
        Assert.True(BackupRules.IsHourlyCopyDue(Now.AddDays(3), Now));
    }

    // A racing autosave callback can run a moment "before" the copy just written; that isn't
    // a clock change and mustn't write a second copy.
    [Fact]
    public void IsHourlyCopyDue_NewestCopySecondsAhead_IsFalse()
    {
        Assert.False(BackupRules.IsHourlyCopyDue(Now.AddSeconds(2), Now));
    }

    [Fact]
    public void FileName_Regular()
    {
        Assert.Equal("scratchpad 2026-11-04 153012.md", Regular(new DateTime(2026, 11, 4, 15, 30, 12)));
    }

    [Fact]
    public void FileName_BeforeBigDelete()
    {
        Assert.Equal("scratchpad 2026-11-04 153012 before big delete.md", BigDelete(new DateTime(2026, 11, 4, 15, 30, 12)));
    }

    [Fact]
    public void TryParseFileName_RoundTripsBothKinds()
    {
        var time = new DateTime(2026, 11, 4, 15, 30, 12);

        Assert.True(BackupRules.TryParseFileName(Regular(time), out var regularTime, out var regularKind));
        Assert.Equal(time, regularTime);
        Assert.Equal(BackupKind.Regular, regularKind);

        Assert.True(BackupRules.TryParseFileName(BigDelete(time), out var bigTime, out var bigKind));
        Assert.Equal(time, bigTime);
        Assert.Equal(BackupKind.BeforeBigDelete, bigKind);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("scratchpad old.md")]
    [InlineData("scratchpad 2026-11-04 1530.md")]
    [InlineData("scratchpad 2026-13-04 153012.md")]
    [InlineData("scratchpad 2026-11-04 153012 before big delete.md.bak")]
    public void TryParseFileName_RejectsOtherNames(string name)
    {
        Assert.False(BackupRules.TryParseFileName(name, out _, out _));
    }

    [Fact]
    public void FilesToDelete_KeepsEverythingWithin48Hours()
    {
        var names = new[]
        {
            Regular(new DateTime(2026, 11, 3, 9, 0, 0)),
            Regular(new DateTime(2026, 11, 3, 10, 0, 0)),
            BigDelete(new DateTime(2026, 11, 3, 10, 5, 0)),
            Regular(new DateTime(2026, 11, 2, 12, 30, 0)),
            Regular(new DateTime(2026, 11, 2, 13, 30, 0))
        };

        Assert.Empty(BackupRules.FilesToDelete(names, Now));
    }

    [Fact]
    public void FilesToDelete_KeepsNewestPerDayBeyond48Hours()
    {
        var morning = Regular(new DateTime(2026, 11, 1, 9, 0, 0));
        var evening = BigDelete(new DateTime(2026, 11, 1, 18, 0, 0));
        var afternoon = Regular(new DateTime(2026, 11, 1, 17, 0, 0));

        var deleted = BackupRules.FilesToDelete(new[] { morning, evening, afternoon }, Now);

        Assert.Equal(new[] { afternoon, morning }.OrderBy(n => n), deleted.OrderBy(n => n));
    }

    [Fact]
    public void FilesToDelete_KeepsDayExactly30DaysBack()
    {
        Assert.Empty(BackupRules.FilesToDelete(new[] { Regular(new DateTime(2026, 10, 5, 10, 0, 0)) }, Now));
    }

    [Fact]
    public void FilesToDelete_DeletesBefore30DayCutoff()
    {
        var old = Regular(new DateTime(2026, 10, 4, 23, 59, 59));

        Assert.Equal(new[] { old }, BackupRules.FilesToDelete(new[] { old }, Now));
    }

    [Fact]
    public void FilesToDelete_NeverIncludesUnparsableNames()
    {
        var names = new[] { "notes.txt", "scratchpad old.md", Regular(new DateTime(2026, 9, 1, 10, 0, 0)) };

        var deleted = BackupRules.FilesToDelete(names, Now);

        Assert.DoesNotContain("notes.txt", deleted);
        Assert.DoesNotContain("scratchpad old.md", deleted);
        Assert.Single(deleted);
    }
}
