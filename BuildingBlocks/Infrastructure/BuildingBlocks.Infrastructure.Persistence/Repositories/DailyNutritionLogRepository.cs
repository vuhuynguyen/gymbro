using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class DailyNutritionLogRepository(AppDbContext context) : IDailyNutritionLogRepository
{
    public async Task AddAsync(DailyNutritionLog log, CancellationToken cancellationToken = default)
        => await context.Set<DailyNutritionLog>().AddAsync(log, cancellationToken);

    // Self-scoped, cross-gym, TRACKED (load-then-mutate). Bypasses the tenant filter and re-applies
    // soft-delete; only ever called with the caller's own id.
    public async Task<DailyNutritionLog?> GetOwnByDateAsync(
        Guid traineeId, DateOnly localDate, CancellationToken cancellationToken = default)
        => await context.Set<DailyNutritionLog>()
            .IgnoreQueryFilters()
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.TraineeId == traineeId && l.LocalDate == localDate && !l.IsDeleted, cancellationToken);

    public IQueryable<DailyNutritionLog> QueryOwnAcrossGyms(Guid traineeId)
        => context.Set<DailyNutritionLog>()
            .IgnoreQueryFilters()
            .Where(l => l.TraineeId == traineeId && !l.IsDeleted);

    public IQueryable<DailyNutritionLog> Query() => context.Set<DailyNutritionLog>().AsQueryable();
}
