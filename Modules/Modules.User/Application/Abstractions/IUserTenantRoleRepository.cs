using BuildingBlocks.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Abstractions;

public interface IUserTenantRoleRepository : IRepository<UserTenantRole>
{
    Task<UserTenantRole?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<UserTenantRole>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserTenantRole>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a transaction-scoped advisory lock that serialises concurrent membership changes for one tenant, so
    /// a read-modify-write such as "the last Owner cannot leave" holds under concurrency. Must be called inside a
    /// transaction (the lock auto-releases on commit/rollback).
    /// </summary>
    Task LockForTenantMembershipChangeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
