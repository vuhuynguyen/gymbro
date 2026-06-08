using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Abstractions;

public interface IWorkoutSessionRepository
{
    Task AddAsync(WorkoutSession session, CancellationToken ct = default);
    Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkoutSession?> GetWithExercisesAndSetsAsync(Guid id, CancellationToken ct = default);
    Task<WorkoutSession?> GetActiveForTraineeAsync(Guid traineeId, CancellationToken ct = default);
    IQueryable<WorkoutSession> Query();

    /// <summary>
    /// Self-scoped, cross-gym read for the unified personal training experience. Deliberately bypasses
    /// the EF tenant filter (via <c>IgnoreQueryFilters</c>) and re-applies soft-delete, returning ONLY
    /// the caller's own sessions across every gym they belong to. Must only ever be called with the
    /// authenticated user's own id — it carries no tenant scoping, so it must never receive a
    /// client-supplied trainee id.
    /// </summary>
    IQueryable<WorkoutSession> QueryOwnAcrossGyms(Guid traineeId);
}
