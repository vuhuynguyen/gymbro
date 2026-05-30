using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Authorization;

public sealed class TenantRoleResolver(IUserTenantRoleRepository roleRepository) : ITenantRoleResolver
{
    public async Task<TenantRole?> GetRoleAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var membership = await roleRepository.GetByUserAndTenantAsync(userId, tenantId, ct);
        return membership?.Role;
    }
}
