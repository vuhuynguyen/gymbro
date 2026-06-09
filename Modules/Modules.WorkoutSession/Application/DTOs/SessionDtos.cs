using Modules.WorkoutPlanModule.Application;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.DTOs;

public sealed record PerformedSetDto(
    Guid Id,
    Guid? PlanSetId,
    int SetNumber,
    PerformedSetType SetType,
    int? Reps,
    decimal? WeightKg,
    int? DurationSeconds,
    int? DistanceM,
    int? Rpe,
    int? RestSeconds,
    bool IsCompleted,
    decimal? EstimatedOneRepMaxKg,
    DateTimeOffset LoggedAt,
    bool IsPr,
    int? Calories = null,
    int? AvgHeartRate = null,
    int? Rounds = null,
    Guid? ParentSetId = null);

public sealed record PerformedExerciseDto(
    Guid Id,
    Guid ExerciseId,
    string? ExerciseName,
    Guid? PlanWorkoutExerciseId,
    Guid? SubstitutedFromExerciseId,
    string? SubstitutedFromExerciseName,
    int Order,
    ExercisePerformStatus Status,
    string? Notes,
    IReadOnlyList<PerformedSetDto> Sets,
    string TrackingType = "Strength",
    Guid? SupersetGroupId = null);

public sealed record SessionSnapshotSetDto(
    Guid PlanSetId,
    int Order,
    PlanSetType SetType,
    int? TargetReps,
    decimal? TargetWeightKg,
    int? TargetRpe,
    int? TargetDurationSeconds,
    int RestSeconds,
    int? TargetDistanceM = null,
    int? TargetRounds = null);

public sealed record SessionSnapshotExerciseDto(
    Guid PlanWorkoutExerciseId,
    Guid ExerciseId,
    string ExerciseName,
    int Order,
    IReadOnlyList<SessionSnapshotSetDto> Sets,
    Guid? SupersetGroupId = null);

public sealed record SessionSnapshotDto(
    string WorkoutName,
    IReadOnlyList<SessionSnapshotExerciseDto> Exercises);

public sealed record SessionStartResultDto(
    Guid SessionId,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    SessionSource Source,
    SessionSnapshotDto? Snapshot);

public sealed record ActiveSessionDto(
    Guid SessionId,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    SessionSource Source,
    SessionSnapshotDto? Snapshot,
    IReadOnlyList<PerformedExerciseDto> Exercises);

public sealed record SessionSummaryDto(
    Guid Id,
    Guid TraineeId,
    string? TraineeName,
    SessionSource Source,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationSeconds,
    int TotalSets,
    int TotalExercises,
    int? RpeOverall,
    Guid? PlanAssignmentId,
    string? WorkoutName,
    decimal TotalVolumeKg,
    int PrCount,
    string? ProgramName,
    int? PlanWeek,
    int? WeeklyGoal);

/// <summary>A working set that established a new estimated-1RM record for its lift in a session.</summary>
public sealed record SessionPrDto(
    Guid ExerciseId,
    string? ExerciseName,
    decimal WeightKg,
    int Reps,
    decimal EstimatedOneRepMaxKg,
    decimal? PreviousEstimatedOneRepMaxKg);

public sealed record SessionListDto(
    IReadOnlyList<SessionSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SessionDetailDto(
    Guid Id,
    Guid TraineeId,
    SessionSource Source,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationSeconds,
    int? RpeOverall,
    decimal? BodyweightKg,
    string? Notes,
    string? ClientTimezone,
    Guid? PlanAssignmentId,
    Guid? PlannedWorkoutId,
    string? WorkoutNameSnapshot,
    IReadOnlyList<PerformedExerciseDto> Exercises,
    SessionSnapshotDto? Snapshot,
    decimal TotalVolumeKg,
    string? ProgramName,
    int? PlanWeek,
    IReadOnlyList<SessionPrDto> Prs);
