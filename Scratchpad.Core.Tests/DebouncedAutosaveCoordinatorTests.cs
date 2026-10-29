using System;
using System.Collections.Generic;
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
}
