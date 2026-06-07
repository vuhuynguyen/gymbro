using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace WebApi.HealthChecks;

/// <summary>
/// Readiness check: verifies Redis when configured. Passes when <c>Cache:Provider=Memory</c> is active.
/// </summary>
public sealed class RedisHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var mux = services.GetService<IConnectionMultiplexer>();
        if (mux is null)
        {
            return HealthCheckResult.Healthy(
                "Redis not configured; Cache:Provider=Memory is active.");
        }

        try
        {
            var latency = await mux.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis reachable ({latency.TotalMilliseconds:F0} ms).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed.", ex);
        }
    }
}
