using BuildingBlocks.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Abstractions;

public interface IInviteRepository : IRepository<Invite>
{
    Task<Invite?> GetActiveByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Invite?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Invite>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Invite?> GetByCodeAndTenantAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);
}
