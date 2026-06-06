using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Per-request memo of tenant-role lookups. A single request resolves the caller's role from the DB on
/// several paths — membership validation in <c>TenantResolutionMiddleware</c>, the
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> permission check, and any handler-level
/// <see cref="ITenantAuthorizationService.CanAccessResourceAsync"/> — which without memoization are 2-4
/// identical <c>UserTenantRole</c> queries. Registered <b>Scoped</b> so the cache lives exactly one request.
/// </summary>
public interface IRequestRoleCache
{
    /// <summary>True if a role (including a cached <c>null</c> = "looked up, not a member") is memoized.</summary>
    bool TryGet(Guid userId, Guid tenantId, out TenantRole? role);

    /// <summary>Memoize the role for this (user, tenant) for the remainder of the request.</summary>
    void Set(Guid userId, Guid tenantId, TenantRole? role);
}
