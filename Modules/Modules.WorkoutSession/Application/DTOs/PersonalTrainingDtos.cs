namespace Modules.WorkoutSessionModule.Application.DTOs;

/// <summary>A lifetime personal record for one lift: the best estimated-1RM working set across all gyms.</summary>
public sealed record PersonalRecordDto(
    Guid ExerciseId,
    string? ExerciseName,
    decimal WeightKg,
    int Reps,
    decimal EstimatedOneRepMaxKg,
    DateTimeOffset AchievedAt);

public sealed record PersonalRecordListDto(
    IReadOnlyList<PersonalRecordDto> Records);

/// <summary>One Monday-anchored training week of the caller's unified history.</summary>
public sealed record ProgressWeekDto(
    DateOnly WeekStart,
    int Sessions,
    int TotalSets,
    decimal TotalVolumeKg);

/// <summary>Unified personal training analytics across every gym the caller participates in.</summary>
public sealed record ProgressDto(
    int TotalSessions,
    int TotalCompletedSessions,
    decimal TotalVolumeKg,
    int TotalSets,
    IReadOnlyList<ProgressWeekDto> Weeks);
