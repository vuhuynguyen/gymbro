using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Infrastructure.Persistence;

public sealed class PerformedSetRepository(DbContext context) : IPerformedSetRepository
{
    public async Task AddAsync(PerformedSet set, CancellationToken ct = default)
        => await context.Set<PerformedSet>().AddAsync(set, ct);

    public async Task<PerformedSet?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<PerformedSet>().FirstOrDefaultAsync(s => s.Id == id, ct);

    public void Remove(PerformedSet set)
        => context.Set<PerformedSet>().Remove(set);

    public IQueryable<PerformedSet> Query()
        => context.Set<PerformedSet>().AsQueryable();
}
