using BuildingBlocks.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Abstractions;

public interface IUserTenantRoleRepository : IRepository<UserTenantRole>
{
    Task<UserTenantRole?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<UserTenantRole>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserTenantRole>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
