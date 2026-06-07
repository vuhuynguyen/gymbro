using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Abstractions;

public interface IPerformedExerciseRepository
{
    Task AddAsync(PerformedExercise exercise, CancellationToken ct = default);
    Task<PerformedExercise?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PerformedExercise?> GetByIdWithSetsAsync(Guid id, CancellationToken ct = default);
    IQueryable<PerformedExercise> Query();
}
