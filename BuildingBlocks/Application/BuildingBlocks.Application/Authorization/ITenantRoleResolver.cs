using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public interface ITenantRoleResolver
{
    Task<TenantRole?> GetRoleAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}
