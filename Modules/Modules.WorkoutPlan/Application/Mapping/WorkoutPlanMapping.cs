using System.Linq.Expressions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Mapping;

internal static class WorkoutPlanMapping
{
    public static PlanSetDetailDto ToPlanSetDetailDto(PlanWorkoutExerciseSet set) =>
        new(
            set.Id,
            set.Order,
            set.SetType,
            set.TargetReps,
            set.TargetWeightKg,
            set.TargetRpe,
            set.TargetDurationSeconds,
            set.RestSeconds,
            set.TargetDistanceM,
            set.TargetRounds);

    public static PlanWorkoutExerciseDetailDto ToPlanWorkoutExerciseDetailDto(
        PlanWorkoutExercise exercise,
        IReadOnlyDictionary<Guid, string> nameById) =>
        new(
            exercise.Id,
            exercise.ExerciseId,
            nameById.GetValueOrDefault(exercise.ExerciseId),
            exercise.Order,
            exercise.PrescribedSets
                .OrderBy(s => s.Order)
                .Select(ToPlanSetDetailDto)
                .ToList(),
            exercise.SupersetGroupId);

    public static PlanWorkoutDetailDto ToPlanWorkoutDetailDto(
        PlanWorkout workout,
        IReadOnlyDictionary<Guid, string> nameById) =>
        new(
            workout.Id,
            workout.Order,
            workout.Name,
            workout.Exercises
                .OrderBy(e => e.Order)
                .Select(e => ToPlanWorkoutExerciseDetailDto(e, nameById))
                .ToList());

    public static WorkoutPlanDetailDto ToWorkoutPlanDetailDto(
        WorkoutPlan plan,
        IReadOnlyDictionary<Guid, string> nameById) =>
        new(
            plan.Id,
            plan.TemplateId,
            plan.Version,
            plan.Name,
            plan.Description,
            plan.DurationWeeks,
            plan.WorkoutsPerWeek,
            plan.CreatedOnUtc,
            plan.Workouts
                .OrderBy(w => w.Order)
                .Select(w => ToPlanWorkoutDetailDto(w, nameById))
                .ToList());

    /// <summary>
    /// Applies a trainee's assignment visibility flags to a plan-detail DTO (filter-on-read for a
    /// <c>Guided</c> assignment). <c>HideExercises</c> removes exercise names/ids in the preview;
    /// <c>HideSetsReps</c> removes prescribed targets (count/type/rest kept); <c>HideFutureWorkouts</c>
    /// returns only the trainee's current program week. Coaches and admins never pass through here.
    /// </summary>
    public static WorkoutPlanDetailDto RedactForTrainee(WorkoutPlanDetailDto dto, PlanAssignment assignment)
    {
        IEnumerable<PlanWorkoutDetailDto> workouts = dto.Workouts.OrderBy(w => w.Order);

        // HideFutureWorkouts: best-effort current-week slice. Needs WorkoutsPerWeek to know the week
        // size; multi-week programs are sliced, single-week plans (and unknown cadence) are unchanged.
        if (assignment.HideFutureWorkouts && dto.WorkoutsPerWeek is { } perWeek && perWeek > 0)
        {
            var ordered = workouts.ToList();
            var totalWeeks = (int)Math.Ceiling(ordered.Count / (double)perWeek);
            if (totalWeeks > 1)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var daysSinceStart = today.DayNumber - assignment.StartDate.DayNumber;
                var currentWeek = daysSinceStart < 0 ? 0 : daysSinceStart / 7;
                currentWeek = Math.Min(currentWeek, totalWeeks - 1);
                workouts = ordered.Skip(currentWeek * perWeek).Take(perWeek);
            }
        }

        var redactedWorkouts = workouts
            .Select(w => new PlanWorkoutDetailDto(
                w.Id,
                w.Order,
                w.Name,
                w.Exercises
                    .Select(e => new PlanWorkoutExerciseDetailDto(
                        e.Id,
                        assignment.HideExercises ? Guid.Empty : e.ExerciseId,
                        assignment.HideExercises ? null : e.ExerciseName,
                        e.Order,
                        e.Sets
                            .Select(s => new PlanSetDetailDto(
                                s.Id,
                                s.Order,
                                s.SetType,
                                assignment.HideSetsReps ? null : s.TargetReps,
                                assignment.HideSetsReps ? null : s.TargetWeightKg,
                                assignment.HideSetsReps ? null : s.TargetRpe,
                                assignment.HideSetsReps ? null : s.TargetDurationSeconds,
                                s.RestSeconds,
                                assignment.HideSetsReps ? null : s.TargetDistanceM,
                                assignment.HideSetsReps ? null : s.TargetRounds))
                            .ToList(),
                        e.SupersetGroupId))
                    .ToList()))
            .ToList();

        return dto with { Workouts = redactedWorkouts };
    }

    public static Expression<Func<WorkoutPlan, WorkoutPlanSummaryDto>> WorkoutPlanSummaryProjection =>
        p => new WorkoutPlanSummaryDto(
            p.Id,
            p.TemplateId,
            p.Version,
            p.Name,
            p.Description,
            p.DurationWeeks,
            p.WorkoutsPerWeek,
            p.CreatedOnUtc,
            p.Workouts.Count,
            p.IsArchived);

    public static WorkoutPlanSummaryDto ToWorkoutPlanSummaryDto(WorkoutPlan plan) =>
        new(
            plan.Id,
            plan.TemplateId,
            plan.Version,
            plan.Name,
            plan.Description,
            plan.DurationWeeks,
            plan.WorkoutsPerWeek,
            plan.CreatedOnUtc,
            plan.Workouts.Count,
            plan.IsArchived);

    public static WorkoutPlanListDto ToWorkoutPlanListDto(
        IReadOnlyList<WorkoutPlanSummaryDto> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(items, page, pageSize, totalCount);

    public static PlanAssignmentSummaryDto ToPlanAssignmentSummaryDto(
        PlanAssignment assignment,
        int? latestVersionInTemplate) =>
        new(
            assignment.Id,
            assignment.TraineeId,
            assignment.PlanId,
            assignment.PlanVersion,
            latestVersionInTemplate ?? assignment.PlanVersion,
            latestVersionInTemplate.HasValue && assignment.PlanVersion < latestVersionInTemplate.Value,
            assignment.StartDate,
            assignment.FrequencyDaysPerWeek,
            assignment.VisibilityMode,
            assignment.HideExercises,
            assignment.HideSetsReps,
            assignment.HideFutureWorkouts,
            assignment.DisableTraineeEditing,
            assignment.IsActive);

    public static PlanAssignmentListDto ToPlanAssignmentListDto(
        IReadOnlyList<PlanAssignmentSummaryDto> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(items, page, pageSize, totalCount);
}
