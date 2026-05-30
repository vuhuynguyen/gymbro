using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Abstractions;

public interface IWorkoutSessionRepository
{
    Task AddAsync(WorkoutSession session, CancellationToken ct = default);
    Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkoutSession?> GetWithExercisesAndSetsAsync(Guid id, CancellationToken ct = default);
    Task<WorkoutSession?> GetActiveForTraineeAsync(Guid traineeId, CancellationToken ct = default);
    IQueryable<WorkoutSession> Query();
}
