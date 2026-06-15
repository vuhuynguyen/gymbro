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

    /// <summary>
    /// The caller's own entries of one metric <paramref name="type"/> over a local-date range, for the
    /// Progress body-metric trend. Self-scoped: only ever called with the caller's own id. <paramref name="type"/>
    /// is matched case-insensitively/normalized (MetricEntry.Type is unvalidated free text). Mirrors the
    /// own-scoped read pattern — IgnoreQueryFilters (defensive, even though MetricEntry is not ITenantEntity)
    /// + an explicit TraineeId scope + an explicit soft-delete predicate.
    /// </summary>
    Task<IReadOnlyList<MetricEntry>> GetOwnSeriesAsync(
        Guid traineeId, string type, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
