using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Scoped (one-per-request) implementation of <see cref="IRequestRoleCache"/>. A present key means the
/// (user, tenant) role was already resolved this request; the stored value may be <c>null</c> to record a
/// confirmed non-membership, so repeated checks never re-query. Not thread-safe by design — one HTTP
/// request is processed on one logical flow.
/// </summary>
public sealed class RequestRoleCache : IRequestRoleCache
{
    private readonly Dictionary<(Guid UserId, Guid TenantId), TenantRole?> _cache = new();

    public bool TryGet(Guid userId, Guid tenantId, out TenantRole? role)
        => _cache.TryGetValue((userId, tenantId), out role);

    public void Set(Guid userId, Guid tenantId, TenantRole? role)
        => _cache[(userId, tenantId)] = role;
}
