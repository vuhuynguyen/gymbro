using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class UserTenantRoleRepository(AppDbContext context)
    : Repository<UserTenantRole>(context), IUserTenantRoleRepository
{
    public async Task<UserTenantRole?> GetByUserAndTenantAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Db.UserTenantRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TenantId == tenantId, cancellationToken);
    }

    public async Task<List<UserTenantRole>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Db.UserTenantRoles
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTenantRole>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Db.UserTenantRoles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
