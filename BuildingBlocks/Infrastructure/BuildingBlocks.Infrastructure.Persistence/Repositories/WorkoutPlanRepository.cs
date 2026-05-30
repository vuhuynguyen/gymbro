using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class WorkoutPlanRepository(AppDbContext context)
    : Repository<WorkoutPlan>(context), IWorkoutPlanRepository
{
    public async Task<WorkoutPlan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Db.Set<WorkoutPlan>()
            .Include(p => p.Workouts)
            .ThenInclude(w => w.Exercises)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<WorkoutPlan?> GetLatestVersionInTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        return await Db.Set<WorkoutPlan>()
            .Where(p => p.TemplateId == templateId)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PlanWorkoutDetailDto?> GetWorkoutForSnapshotAsync(Guid workoutId, CancellationToken ct = default)
    {
        var workout = await Db.Set<PlanWorkout>()
            .AsNoTracking()
            .Include(w => w.Exercises)
            .ThenInclude(e => e.PrescribedSets)
            .FirstOrDefaultAsync(w => w.Id == workoutId, ct);

        if (workout == null)
            return null;

        var exerciseIds = workout.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseNames = await Db.Set<Modules.ExerciseModule.Entities.Exercise>()
            .AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .Select(e => new { e.Id, e.DefaultName })
            .ToDictionaryAsync(e => e.Id, e => e.DefaultName, ct);

        var exerciseDtos = workout.Exercises
            .OrderBy(e => e.Order)
            .Select(e => new PlanWorkoutExerciseDetailDto(
                e.Id,
                e.ExerciseId,
                exerciseNames.TryGetValue(e.ExerciseId, out var name) ? name : null,
                e.Order,
                e.PrescribedSets
                    .OrderBy(s => s.Order)
                    .Select(s => new PlanSetDetailDto(
                        s.Id, s.Order, s.SetType,
                        s.TargetReps, s.TargetWeightKg, s.TargetRpe,
                        s.TargetDurationSeconds, s.RestSeconds))
                    .ToList()))
            .ToList();

        return new PlanWorkoutDetailDto(workout.Id, workout.Order, workout.Name, exerciseDtos);
    }

    public async Task ClearPlanStructureAsync(Guid workoutPlanId, CancellationToken cancellationToken = default)
    {
        var trackedWorkoutEntries = Db.ChangeTracker
            .Entries<PlanWorkout>()
            .Where(e => e.Entity.WorkoutPlanId == workoutPlanId)
            .ToList();

        var workoutIds = await Db.Set<PlanWorkout>()
            .AsNoTracking()
            .Where(w => w.WorkoutPlanId == workoutPlanId)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        var allWorkoutIds = workoutIds
            .Concat(trackedWorkoutEntries.Select(e => e.Entity.Id))
            .Distinct()
            .ToList();

        if (allWorkoutIds.Count > 0)
        {
            var trackedExerciseEntries = Db.ChangeTracker
                .Entries<PlanWorkoutExercise>()
                .Where(e => allWorkoutIds.Contains(e.Entity.PlanWorkoutId))
                .ToList();

            foreach (var trackedExerciseEntry in trackedExerciseEntries)
                trackedExerciseEntry.State = EntityState.Detached;
        }

        foreach (var trackedWorkoutEntry in trackedWorkoutEntries)
            trackedWorkoutEntry.State = EntityState.Detached;

        if (workoutIds.Count > 0)
        {
            await Db.Set<PlanWorkoutExercise>()
                .Where(e => workoutIds.Contains(e.PlanWorkoutId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await Db.Set<PlanWorkout>()
            .Where(w => w.WorkoutPlanId == workoutPlanId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
