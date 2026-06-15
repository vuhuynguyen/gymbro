using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Infrastructure.Persistence;

public sealed class MetricEntryRepository(DbContext context) : IMetricEntryRepository
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

    // Own-scoped series read for the Progress body-metric trend. Type is matched case-insensitively (it is
    // unvalidated free text). IgnoreQueryFilters + the explicit TraineeId scope + the explicit !IsDeleted
    // predicate mirror the cross-gym own-scoped pattern (and keep the soft-delete guarantee under the bypass).
    public async Task<IReadOnlyList<MetricEntry>> GetOwnSeriesAsync(
        Guid traineeId, string type, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        return await context.Set<MetricEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => !e.IsDeleted
                && e.TraineeId == traineeId
                && e.Type.ToLower() == normalized
                && e.LocalDate >= from
                && e.LocalDate <= to)
            .OrderBy(e => e.LocalDate)
            .ThenBy(e => e.LoggedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
