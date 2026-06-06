using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Authorization;

public sealed class TenantRoleResolver(
    IUserTenantRoleRepository roleRepository,
    IRequestRoleCache roleCache) : ITenantRoleResolver
{
    public async Task<TenantRole?> GetRoleAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        // Reuse the per-request lookup if one already happened (middleware membership check, an earlier
        // permission check, etc.) — a present entry includes a cached null = "looked up, not a member".
        if (roleCache.TryGet(userId, tenantId, out var cached))
            return cached;

        var membership = await roleRepository.GetByUserAndTenantAsync(userId, tenantId, ct);
        var role = membership?.Role;
        roleCache.Set(userId, tenantId, role);
        return role;
    }
}
