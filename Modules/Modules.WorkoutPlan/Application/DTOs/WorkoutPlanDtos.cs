using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.DTOs;

public sealed record WorkoutPlanSummaryDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek,
    DateTimeOffset CreatedOnUtc,
    int WorkoutCount,
    bool IsArchived);

public sealed record WorkoutPlanListDto(
    IReadOnlyList<WorkoutPlanSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlanSetDetailDto(
    Guid Id,
    int Order,
    PlanSetType SetType,
    int? TargetReps,
    decimal? TargetWeightKg,
    int? TargetRpe,
    int? TargetDurationSeconds,
    int RestSeconds,
    int? TargetDistanceM = null,
    int? TargetRounds = null);

public sealed record PlanWorkoutExerciseDetailDto(
    Guid Id,
    Guid ExerciseId,
    string? ExerciseName,
    int Order,
    IReadOnlyList<PlanSetDetailDto> Sets,
    Guid? SupersetGroupId = null);

public sealed record PlanWorkoutDetailDto(
    Guid Id,
    int Order,
    string Name,
    IReadOnlyList<PlanWorkoutExerciseDetailDto> Exercises);

public sealed record WorkoutPlanDetailDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string Name,
    string? Description,
    int? DurationWeeks,
    int? WorkoutsPerWeek,
    DateTimeOffset CreatedOnUtc,
    IReadOnlyList<PlanWorkoutDetailDto> Workouts);
