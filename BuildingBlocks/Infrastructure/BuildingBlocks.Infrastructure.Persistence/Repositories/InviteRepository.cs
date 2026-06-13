using Microsoft.EntityFrameworkCore;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class InviteRepository(AppDbContext context) : Repository<Invite>(context), IInviteRepository
{
    public async Task<Invite?> GetActiveByEmailAndTenantAsync(
        string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await Db.Invites
            .FirstOrDefaultAsync(
                i => i.Email == email.ToLowerInvariant()
                     && i.TenantId == tenantId
                     && !i.IsUsed
                     && i.ExpiredAt > now,
                cancellationToken);
    }

    public async Task<Invite?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await Db.Invites
            .FirstOrDefaultAsync(
                i => i.Code == code.ToUpperInvariant()
                     && !i.IsUsed
                     && i.ExpiredAt > now,
                cancellationToken);
    }

    public async Task<List<Invite>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await Db.Invites
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task<Invite?> GetByCodeAndTenantAsync(string code, Guid tenantId, CancellationToken cancellationToken = default) =>
        await Db.Invites
            .FirstOrDefaultAsync(
                i => i.Code == code.ToUpperInvariant() && i.TenantId == tenantId,
                cancellationToken);

    public async Task<bool> TryClaimAsync(Guid inviteId, CancellationToken cancellationToken = default)
    {
        // Single conditional UPDATE: only the row that is still unused is claimed, so two concurrent
        // redemptions of the same invite cannot both succeed (the loser updates 0 rows).
        var affected = await Db.Invites
            .Where(i => i.Id == inviteId && !i.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsUsed, true), cancellationToken);

        return affected > 0;
    }
}
