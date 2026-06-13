using BuildingBlocks.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Abstractions;

public interface IInviteRepository : IRepository<Invite>
{
    Task<Invite?> GetActiveByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Invite?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Invite>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Invite?> GetByCodeAndTenantAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks a still-unused invite as used, returning whether THIS call claimed it. A conditional
    /// UPDATE (… WHERE NOT IsUsed) means at most one concurrent redemption wins, enforcing single-use without a
    /// read-check-then-mark race.
    /// </summary>
    Task<bool> TryClaimAsync(Guid inviteId, CancellationToken cancellationToken = default);
}
