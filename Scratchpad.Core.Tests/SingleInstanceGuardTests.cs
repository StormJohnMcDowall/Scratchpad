using System;
using System.Threading;
using Scratchpad.Core;
using Xunit;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuard_FailsToAcquire_WhenFirstHoldsMutex()
    {
        var mutexName = "Test-" + Guid.NewGuid();
        var pipeName = "Test-" + Guid.NewGuid();
        using var first = new SingleInstanceGuard(mutexName, pipeName);
        using var second = new SingleInstanceGuard(mutexName, pipeName);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void NotifyExistingInstance_InvokesCallbackOnFirstGuard()
    {
        var mutexName = "Test-" + Guid.NewGuid();
        var pipeName = "Test-" + Guid.NewGuid();
        using var first = new SingleInstanceGuard(mutexName, pipeName);
        using var second = new SingleInstanceGuard(mutexName, pipeName);
        var signal = new ManualResetEventSlim();
        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
        first.ListenForShowRequests(() => signal.Set());

        second.NotifyExistingInstanceToShow();

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
    }
}
