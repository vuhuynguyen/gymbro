using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public class TenantAuthorizationService(
    ITenantRoleResolver roleResolver,
    IPermissionService permissionService,
    ICurrentUser currentUser) : ITenantAuthorizationService
{
    public async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await roleResolver.GetRoleAsync(currentUser.UserId, tenantId, ct);
    }

    public async Task<bool> HasPermissionAsync(Guid tenantId, Permission permission, CancellationToken ct = default)
    {
        var role = await GetRoleAsync(tenantId, ct);
        return role.HasValue && permissionService.HasPermission(role.Value, permission);
    }

    public async Task<bool> CanAccessResourceAsync(
        Guid tenantId,
        Permission ownPermission,
        Permission allPermission,
        Guid resourceUserId,
        CancellationToken ct = default)
    {
        var role = await GetRoleAsync(tenantId, ct);
        if (!role.HasValue) return false;

        if (permissionService.HasPermission(role.Value, allPermission)) return true;

        return permissionService.HasPermission(role.Value, ownPermission)
               && resourceUserId == currentUser.UserId;
    }
}
