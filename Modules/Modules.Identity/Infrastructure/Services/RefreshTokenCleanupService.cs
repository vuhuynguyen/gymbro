using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Infrastructure.Services;

/// <summary>
/// Periodically deletes refresh tokens that are past their expiry, so the <c>RefreshTokens</c> table
/// does not grow unbounded. Expired rows carry no security value: an expired token fails validation
/// regardless of its spent/revoked state, and a token's whole rotation family dies once its head
/// expires — so reuse detection for live families is unaffected. Spent/revoked tokens that have not
/// yet expired are intentionally retained for reuse detection.
/// </summary>
public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RefreshTokenCleanupService> logger)
    : BackgroundService
{
    private TimeSpan Interval =>
        TimeSpan.FromHours(configuration.GetValue("Jwt:RefreshTokenCleanupIntervalHours", 6));

    /// <summary>Grace period kept past expiry before a row is purged (audit/debug headroom).</summary>
    private TimeSpan Retention =>
        TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenRetentionDays", 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sweep once at startup, then on the configured cadence.
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a transient failure tear down the host; just try again next tick.
                logger.LogError(ex, "Refresh-token cleanup sweep failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - Retention;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var removed = await db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
            logger.LogInformation("Refresh-token cleanup removed {Count} expired token(s).", removed);
    }
}
