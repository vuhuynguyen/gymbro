using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Modules.IdentityModule.Infrastructure.Identity;

namespace WebApi.HealthChecks;

/// <summary>
/// Readiness check: verifies both EF contexts can reach Postgres. Uses the framework's health-check
/// abstraction (no third-party package) and <c>CanConnectAsync</c> on each context, so it covers the two
/// migration chains the app depends on. Tagged "ready" so it is served on <c>/health/ready</c> only —
/// <c>/health</c> stays a dependency-free liveness probe.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext appDb, IdentityDbContext identityDb) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appReachable = await appDb.Database.CanConnectAsync(cancellationToken);
            var identityReachable = await identityDb.Database.CanConnectAsync(cancellationToken);

            return appReachable && identityReachable
                ? HealthCheckResult.Healthy("Both database contexts are reachable.")
                : HealthCheckResult.Unhealthy(
                    $"Database unreachable (app={appReachable}, identity={identityReachable}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity check threw.", ex);
        }
    }
}
