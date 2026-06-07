using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Abstractions;

public interface IPerformedSetRepository
{
    Task AddAsync(PerformedSet set, CancellationToken ct = default);
    Task<PerformedSet?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Remove(PerformedSet set);
    IQueryable<PerformedSet> Query();
}
