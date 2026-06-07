using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace WebApi.Composition;

/// <summary>
/// Background polling loop that drains the transactional outbox: every <c>PollInterval</c> it dispatches a
/// batch of pending domain events via <see cref="OutboxDispatcher"/> in a fresh DI scope. Failures are
/// logged and retried on the next tick; a batch error never stops the loop.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
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
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down — nothing to do.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox processing batch failed; will retry on the next tick.");
        }
    }
}
