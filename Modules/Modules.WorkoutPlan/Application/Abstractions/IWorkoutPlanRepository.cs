using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Abstractions;

public interface IWorkoutPlanRepository
{
    Task AddAsync(WorkoutPlan entity, CancellationToken cancellationToken = default);

    Task<WorkoutPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkoutPlan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The version head for a template — the draft if one exists, else the latest published version.</summary>
    Task<WorkoutPlan?> GetLatestVersionInTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>The newest <b>published</b> version in a template (ignores any draft head). Null when never published.</summary>
    Task<WorkoutPlan?> GetLatestPublishedVersionInTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>Hard-removes a plan row (used to drop a superseded draft head when replacing it).</summary>
    void Remove(WorkoutPlan entity);

    /// <summary>
    /// Removes all workouts and line items for the plan using bulk SQL (no tracked deletes), avoiding
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> when rows were already removed.
    /// </summary>
    Task ClearPlanStructureAsync(Guid workoutPlanId, CancellationToken cancellationToken = default);

    IQueryable<WorkoutPlan> Query();

    /// <summary>
    /// Loads a single PlanWorkout with its exercises and prescribed sets for session snapshot generation.
    /// Exercise names are resolved cross-module by the handler (via <c>ResolveExerciseNamesQuery</c>), not here.
    /// </summary>
    Task<PlanWorkout?> GetWorkoutWithExercisesAsync(Guid workoutId, CancellationToken ct = default);
}
