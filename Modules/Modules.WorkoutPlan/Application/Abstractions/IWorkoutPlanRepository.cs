using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Abstractions;

public interface IWorkoutPlanRepository
{
    Task AddAsync(WorkoutPlan entity, CancellationToken cancellationToken = default);

    Task<WorkoutPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkoutPlan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkoutPlan?> GetLatestVersionInTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all workouts and line items for the plan using bulk SQL (no tracked deletes), avoiding
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> when rows were already removed.
    /// </summary>
    Task ClearPlanStructureAsync(Guid workoutPlanId, CancellationToken cancellationToken = default);

    IQueryable<WorkoutPlan> Query();

    /// <summary>Loads a single PlanWorkout with exercises, prescribed sets, and exercise names for session snapshot generation.</summary>
    Task<PlanWorkoutDetailDto?> GetWorkoutForSnapshotAsync(Guid workoutId, CancellationToken ct = default);
}
