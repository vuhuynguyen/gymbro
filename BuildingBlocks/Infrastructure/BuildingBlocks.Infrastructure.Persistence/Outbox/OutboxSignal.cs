using System.Threading.Channels;

namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Channel-backed <see cref="IOutboxSignal"/>, registered as a singleton so every DbContext scope shares the
/// one instance the <c>OutboxProcessor</c> waits on. A bounded capacity-1 channel with
/// <see cref="BoundedChannelFullMode.DropWrite"/> coalesces a burst of notifications into a single pending
/// wake, and an idle app produces no signal — and therefore no database access — at all.
/// </summary>
public sealed class OutboxSignal : IOutboxSignal
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            // A second write while one wake is already pending is dropped: the processor drains everything
            // available on its next pass anyway, so one pending token is all that is ever needed.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,  // only the OutboxProcessor reads
            SingleWriter = false, // many request/background scopes may Notify concurrently
        });

    public void Notify() => _channel.Writer.TryWrite(0);

    public async Task WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(fallbackInterval);

        try
        {
            // Blocks with no work (and no DB access) until Notify writes a token, or the fallback fires.
            await _channel.Reader.ReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (
            timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Fallback interval elapsed with no write signal — return normally so the caller runs its
            // safety-net drain. Host-shutdown cancellation (cancellationToken) is NOT swallowed: it
            // propagates so the processor loop can exit.
        }
    }
}
