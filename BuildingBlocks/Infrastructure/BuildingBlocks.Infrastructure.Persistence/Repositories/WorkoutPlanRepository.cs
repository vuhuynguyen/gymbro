using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
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

    public async Task<PlanWorkout?> GetWorkoutWithExercisesAsync(Guid workoutId, CancellationToken ct = default)
    {
        return await Db.Set<PlanWorkout>()
            .AsNoTracking()
            .Include(w => w.Exercises)
            .ThenInclude(e => e.PrescribedSets)
            .FirstOrDefaultAsync(w => w.Id == workoutId, ct);
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
