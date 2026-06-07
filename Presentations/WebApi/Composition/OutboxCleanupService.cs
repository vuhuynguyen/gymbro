using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace WebApi.Composition;

/// <summary>
/// Periodically purges processed outbox rows older than <c>Outbox:Retention</c> so the table stays bounded.
/// Runs on its own (slower) cadence, independent of the dispatch loop.
/// </summary>
public sealed class OutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxCleanupService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);

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

            var cutoff = DateTime.UtcNow - _options.Retention;
            var purged = await dispatcher.PurgeProcessedAsync(cutoff, stoppingToken);

            if (purged > 0)
                logger.LogInformation("Outbox purged {Count} processed message(s) older than {Cutoff:o}.", purged, cutoff);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox cleanup failed; will retry on the next tick.");
        }
    }
}
