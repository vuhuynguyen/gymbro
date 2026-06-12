using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class DailyNutritionLogRepository(AppDbContext context) : IDailyNutritionLogRepository
{
    public async Task AddAsync(DailyNutritionLog log, CancellationToken cancellationToken = default)
        => await context.Set<DailyNutritionLog>().AddAsync(log, cancellationToken);

    // Force the new child into the Added state. Adding it only via the parent's _items collection suffices
    // when the parent is itself Added (its whole graph inserts), but NOT when the parent was loaded
    // (Unchanged): EF then infers the app-assigned Guid key as an existing row and emits a 0-row UPDATE.
    public void AddItem(LoggedItem item)
        => context.Set<LoggedItem>().Add(item);

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

    public void Detach(DailyNutritionLog log)
    {
        foreach (var item in log.Items)
            context.Entry(item).State = EntityState.Detached;
        context.Entry(log).State = EntityState.Detached;
    }
}
