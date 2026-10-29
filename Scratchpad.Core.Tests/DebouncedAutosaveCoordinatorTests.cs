using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Scratchpad.Core;
using Xunit;

public class DebouncedAutosaveCoordinatorTests
{
    [Fact]
    public void CoalescesRapidChangesIntoASingleSave()
    {
        var saves = new List<string>();
        var signal = new ManualResetEventSlim();
        using var coordinator = new DebouncedAutosaveCoordinator(
            s => { saves.Add(s); signal.Set(); }, TimeSpan.FromMilliseconds(50));

        coordinator.NotifyChanged("a");
        coordinator.NotifyChanged("ab");
        coordinator.NotifyChanged("abc");

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Thread.Sleep(100); // ensure no further saves arrive
        Assert.Equal(new[] { "abc" }, saves);
    }

    [Fact]
    public void SavesAgainAfterDelayElapsesBetweenChanges()
    {
        var saves = new List<string>();
        var signal = new ManualResetEventSlim();
        using var coordinator = new DebouncedAutosaveCoordinator(
            s => { saves.Add(s); signal.Set(); }, TimeSpan.FromMilliseconds(50));

        coordinator.NotifyChanged("a");
        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        signal.Reset();
        coordinator.NotifyChanged("b");
        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));

        Assert.Equal(new[] { "a", "b" }, saves);
    }

    [Fact]
    public void SaveExceptionDoesNotCrashCoordinator_AndRetriesAfterDelay()
    {
        var attempts = 0;
        var saves = new List<string>();
        var signal = new ManualResetEventSlim();
        using var coordinator = new DebouncedAutosaveCoordinator(s =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new IOException("simulated transient failure");
            }
            saves.Add(s);
            signal.Set();
        }, TimeSpan.FromMilliseconds(50));

        coordinator.NotifyChanged("a");

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { "a" }, saves);
        Assert.True(attempts >= 2);
    }

    [Fact]
    public void Flush_ImmediatelySavesPendingChangeWithoutWaitingForDelay()
    {
        var saves = new List<string>();
        using var coordinator = new DebouncedAutosaveCoordinator(s => saves.Add(s), TimeSpan.FromSeconds(10));

        coordinator.NotifyChanged("pending");
        coordinator.Flush();

        Assert.Equal(new[] { "pending" }, saves);
    }

    [Fact]
    public void Flush_DoesNothingWhenNoPendingChange()
    {
        var saves = new List<string>();
        using var coordinator = new DebouncedAutosaveCoordinator(s => saves.Add(s), TimeSpan.FromMilliseconds(50));

        coordinator.Flush();

        Assert.Empty(saves);
    }

    [Fact]
    public void NotifyChanged_AfterDispose_DoesNotThrow()
    {
        var coordinator = new DebouncedAutosaveCoordinator(s => { }, TimeSpan.FromMilliseconds(50));
        coordinator.Dispose();

        var ex = Record.Exception(() => coordinator.NotifyChanged("x"));

        Assert.Null(ex);
    }
}
