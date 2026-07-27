using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace WebApi.Composition;

/// <summary>
/// Drains the transactional outbox. Rather than polling the database on a fixed interval — which would keep a
/// serverless Postgres (e.g. Neon) permanently awake and prevent it scaling to zero — it blocks on an
/// in-process <see cref="IOutboxSignal"/> that each committing write raises, and dispatches immediately.
/// A slow <c>FallbackInterval</c> still forces a periodic safety-net drain (crash recovery, a missed signal,
/// or events written by another instance). Failures are logged and retried on the next signal/fallback tick;
/// a batch error never stops the loop.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOutboxSignal signal,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Catch up on anything left pending from a previous run — in-process signals do not survive a restart.
        await DrainAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Block with NO database access until a write signals work (immediate dispatch) or the slow
                // safety-net interval elapses. This idle-with-no-queries state is what lets Neon scale to zero.
                await signal.WaitAsync(_options.FallbackInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await DrainAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Dispatches batches until the backlog is cleared. A full successful batch implies more may be waiting,
    /// so we loop; a short batch (fewer than <c>BatchSize</c> published, e.g. because some failed) stops the
    /// loop so poison messages are retried on a later tick rather than hammered here.
    /// </summary>
    private async Task DrainAsync(CancellationToken stoppingToken)
    {
        int dispatched;
        do
        {
            dispatched = await RunBatchAsync(stoppingToken);
        }
        while (dispatched == _options.BatchSize && !stoppingToken.IsCancellationRequested);
    }

    private async Task<int> RunBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

            var dispatched = await dispatcher.ProcessBatchAsync(
                _options.BatchSize,
                _options.MaxAttempts,
                stoppingToken);

            if (dispatched > 0)
                logger.LogDebug("Outbox dispatched {Count} message(s).", dispatched);

            return dispatched;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down — nothing to do.
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox processing batch failed; will retry on the next signal or fallback tick.");
            return 0;
        }
    }
}
