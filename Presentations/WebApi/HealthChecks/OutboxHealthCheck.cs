using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace WebApi.HealthChecks;

/// <summary>
/// Surfaces poison/dead-lettered outbox messages (unprocessed and past <c>MaxAttempts</c>) so they are
/// visible to operators instead of silently stuck. Reports <see cref="HealthStatus.Degraded"/> — not
/// Unhealthy — once the dead-letter count reaches the configured threshold, so it appears on the readiness
/// dashboard without failing the probe and pulling the instance out of rotation.
/// </summary>
public sealed class OutboxHealthCheck(OutboxDispatcher dispatcher, IOptions<OutboxOptions> options) : IHealthCheck
{
    private readonly OutboxOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deadLettered = await dispatcher.CountDeadLetteredAsync(_options.MaxAttempts, cancellationToken);
        var data = new Dictionary<string, object> { ["deadLettered"] = deadLettered };

        return deadLettered >= _options.DeadLetterAlertThreshold
            ? HealthCheckResult.Degraded($"{deadLettered} outbox message(s) dead-lettered.", data: data)
            : HealthCheckResult.Healthy("No dead-lettered outbox messages.", data);
    }
}
