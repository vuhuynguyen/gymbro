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
            set.RestSeconds);

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
                .ToList());

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
            p.Workouts.Count);

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
            plan.Workouts.Count);

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
            assignment.IsCustomized);

    public static PlanAssignmentListDto ToPlanAssignmentListDto(
        IReadOnlyList<PlanAssignmentSummaryDto> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(items, page, pageSize, totalCount);
}
