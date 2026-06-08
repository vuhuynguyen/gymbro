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
        Guid? resourceTenantId = null,
        CancellationToken ct = default)
    {
        var role = await GetRoleAsync(tenantId, ct);
        if (!role.HasValue) return false;

        // ViewAll (coach/owner) is bounded to the caller's OWN gym: a tenant-wide permission must never
        // reach a resource that lives in another tenant. When the resource's tenant is unknown (null),
        // fall back to the legacy permissive behavior — the EF tenant filter still scopes the load there.
        if (permissionService.HasPermission(role.Value, allPermission))
            return resourceTenantId is null || resourceTenantId == tenantId;

        // Own access: the resource belongs to the caller regardless of which gym it lives in.
        return permissionService.HasPermission(role.Value, ownPermission)
               && resourceUserId == currentUser.UserId;
    }
}
