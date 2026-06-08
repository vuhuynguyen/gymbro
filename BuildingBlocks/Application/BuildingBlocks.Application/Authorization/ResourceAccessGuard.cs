using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Shared trainee-scoped workout log access via <see cref="ITenantAuthorizationService.CanAccessResourceAsync"/>.
/// Does not validate tenant context or load resources — callers keep those concerns.
/// </summary>
public static class ResourceAccessGuard
{
    public static Task<bool> CanViewTraineeWorkoutLogsAsync(
        ITenantAuthorizationService tenantAuth,
        Guid tenantId,
        Guid traineeId,
        Guid? resourceTenantId = null,
        CancellationToken ct = default) =>
        tenantAuth.CanAccessResourceAsync(
            tenantId,
            Permission.WorkoutLogViewOwn,
            Permission.WorkoutLogViewAll,
            traineeId,
            resourceTenantId,
            ct);
}
