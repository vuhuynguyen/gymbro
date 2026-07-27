using System.Diagnostics;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Xunit;

namespace Gymbro.Tests.Outbox;

/// <summary>
/// Verifies the in-process wake signal that replaced fixed-interval outbox polling: a write wakes the
/// processor immediately, an idle wait falls back only after the interval (never earlier), a burst of
/// notifications coalesces into a single wake, and host-shutdown cancellation propagates. Together these are
/// what let the database stay idle between writes so a serverless Postgres can scale to zero.
/// </summary>
public sealed class OutboxSignalTests
{
    [Fact]
    public async Task Notify_before_wait_wakes_immediately()
    {
        var signal = new OutboxSignal();
        signal.Notify();

        var sw = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        sw.Stop();

        // Woke on the pending signal, not the 30s fallback.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Expected an immediate wake, took {sw.Elapsed}.");
    }

    [Fact]
    public async Task Notify_during_a_wait_wakes_it()
    {
        var signal = new OutboxSignal();

        var waiter = signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.False(waiter.IsCompleted); // parked on the long fallback, no signal yet

        signal.Notify();

        // Completes via the signal, well before the 30s fallback would fire.
        await waiter.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Wait_without_a_signal_returns_after_the_fallback_without_throwing()
    {
        var signal = new OutboxSignal();

        var sw = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None); // must not throw
        sw.Stop();

        // The fallback timer never fires early, so the wait lasts at least (about) the interval.
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(150), $"Returned too early: {sw.Elapsed}.");
    }

    [Fact]
    public async Task Signals_coalesce_into_a_single_wake()
    {
        var signal = new OutboxSignal();
        signal.Notify();
        signal.Notify();
        signal.Notify();

        // First wait consumes the single coalesced token immediately.
        var first = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        first.Stop();
        Assert.True(first.Elapsed < TimeSpan.FromSeconds(5), $"First wake should be immediate, took {first.Elapsed}.");

        // No second token remains — the next wait must fall back on the interval.
        var second = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        second.Stop();
        Assert.True(second.Elapsed >= TimeSpan.FromMilliseconds(150), $"Signals did not coalesce: {second.Elapsed}.");
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        var signal = new OutboxSignal();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => signal.WaitAsync(TimeSpan.FromSeconds(30), cts.Token));
    }
}
