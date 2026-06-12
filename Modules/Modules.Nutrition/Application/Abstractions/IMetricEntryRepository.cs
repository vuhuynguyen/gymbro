using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Abstractions;

public interface IMetricEntryRepository
{
    Task AddAsync(MetricEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's own metric entries for one local date, NEWEST FIRST (the client takes the first entry
    /// per type as "latest"). Self-scoped: only ever called with the caller's own id. MetricEntry carries
    /// no tenant filter (see the entity doc), so no IgnoreQueryFilters is needed.
    /// </summary>
    Task<IReadOnlyList<MetricEntry>> GetOwnForDateAsync(
        Guid traineeId, DateOnly localDate, CancellationToken cancellationToken = default);
}
