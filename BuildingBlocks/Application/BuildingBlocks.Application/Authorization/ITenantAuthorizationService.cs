using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public interface ITenantAuthorizationService
{
    Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct = default);

    Task<bool> HasPermissionAsync(Guid tenantId, Permission permission, CancellationToken ct = default);

    Task<bool> CanAccessResourceAsync(
        Guid tenantId,
        Permission ownPermission,
        Permission allPermission,
        Guid resourceUserId,
        CancellationToken ct = default);
}
