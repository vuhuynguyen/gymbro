using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class MetricEntryRepository(AppDbContext context) : IMetricEntryRepository
{
    public async Task AddAsync(MetricEntry entry, CancellationToken cancellationToken = default)
        => await context.Set<MetricEntry>().AddAsync(entry, cancellationToken);

    // Self-scoped: only ever called with the caller's own id. MetricEntry has no tenant filter (it is not
    // ITenantEntity), so the soft-delete global filter alone applies — no IgnoreQueryFilters needed.
    public async Task<IReadOnlyList<MetricEntry>> GetOwnForDateAsync(
        Guid traineeId, DateOnly localDate, CancellationToken cancellationToken = default)
        => await context.Set<MetricEntry>()
            .AsNoTracking()
            .Where(e => e.TraineeId == traineeId && e.LocalDate == localDate)
            .OrderByDescending(e => e.LoggedAtUtc)
            .ToListAsync(cancellationToken);
}
