using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Infrastructure.Persistence;

public class UserTenantRoleRepository(DbContext context)
    : Repository<UserTenantRole>(context), IUserTenantRoleRepository
{
    public async Task<UserTenantRole?> GetByUserAndTenantAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Db.Set<UserTenantRole>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TenantId == tenantId, cancellationToken);
    }

    public async Task<List<UserTenantRole>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Db.Set<UserTenantRole>()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTenantRole>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Db.Set<UserTenantRole>()
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task LockForTenantMembershipChangeAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        // Transaction-scoped advisory lock keyed on the tenant; concurrent membership changes for the same
        // tenant serialise on it and auto-release at commit/rollback.
        Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenantId}::text, 0))", cancellationToken);
}
