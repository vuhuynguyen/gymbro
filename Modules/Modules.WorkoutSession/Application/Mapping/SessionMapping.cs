using System.Text.Json;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Mapping;

internal static class SessionMapping
{
    public static SessionSnapshotDto? DeserializeSnapshot(string? json) =>
        json != null ? JsonSerializer.Deserialize<SessionSnapshotDto>(json) : null;

    public static IReadOnlyList<Guid> CollectExerciseIds(IEnumerable<PerformedExercise> exercises) =>
        exercises
            .SelectMany(e => new[] { e.ExerciseId, e.SubstitutedFromExerciseId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    public static PerformedSetDto ToPerformedSetDto(PerformedSet set) =>
        new(
            set.Id,
            set.PlanSetId,
            set.SetNumber,
            set.SetType,
            set.Reps,
            set.WeightKg,
            set.DurationSeconds,
            set.DistanceM,
            set.Rpe,
            set.RestSeconds,
            set.IsCompleted,
            set.EstimatedOneRepMaxKg,
            set.LoggedAt);

    public static PerformedExerciseDto ToPerformedExerciseDto(
        PerformedExercise exercise,
        IReadOnlyDictionary<Guid, string>? nameById = null)
    {
        nameById ??= new Dictionary<Guid, string>();

        return new PerformedExerciseDto(
            exercise.Id,
            exercise.ExerciseId,
            nameById.GetValueOrDefault(exercise.ExerciseId),
            exercise.PlanWorkoutExerciseId,
            exercise.SubstitutedFromExerciseId,
            exercise.SubstitutedFromExerciseId is { } subId
                ? nameById.GetValueOrDefault(subId)
                : null,
            exercise.Order,
            exercise.Status,
            exercise.Notes,
            exercise.Sets
                .OrderBy(s => s.SetNumber)
                .Select(ToPerformedSetDto)
                .ToList());
    }

    public static IReadOnlyList<PerformedExerciseDto> ToPerformedExerciseDtos(
        IEnumerable<PerformedExercise> exercises,
        IReadOnlyDictionary<Guid, string> nameById) =>
        exercises
            .OrderBy(e => e.Order)
            .Select(e => ToPerformedExerciseDto(e, nameById))
            .ToList();

    public static SessionSnapshotDto BuildSnapshot(PlanWorkoutDetailDto workout)
    {
        var exercises = workout.Exercises
            .Select(e => new SessionSnapshotExerciseDto(
                e.Id,
                e.ExerciseId,
                e.ExerciseName ?? string.Empty,
                e.Order,
                e.Sets
                    .Select(s => new SessionSnapshotSetDto(
                        s.Id,
                        s.Order,
                        s.SetType.ToString(),
                        s.TargetReps,
                        s.TargetWeightKg,
                        s.TargetRpe,
                        s.TargetDurationSeconds,
                        s.RestSeconds))
                    .ToList()))
            .ToList();

        return new SessionSnapshotDto(workout.Name, exercises);
    }

    public static ActiveSessionDto ToActiveSessionDto(
        WorkoutSession session,
        SessionSnapshotDto? snapshot,
        IReadOnlyDictionary<Guid, string> nameById) =>
        new(
            session.Id,
            session.Status,
            session.StartedAt,
            session.Source,
            snapshot,
            ToPerformedExerciseDtos(session.Exercises, nameById));

    public static SessionDetailDto ToSessionDetailDto(
        WorkoutSession session,
        SessionSnapshotDto? snapshot,
        IReadOnlyDictionary<Guid, string> nameById) =>
        new(
            session.Id,
            session.TraineeId,
            session.Source,
            session.Status,
            session.StartedAt,
            session.CompletedAt,
            session.DurationSeconds,
            session.RpeOverall,
            session.BodyweightKg,
            session.Notes,
            session.ClientTimezone,
            session.PlanAssignmentId,
            session.PlannedWorkoutId,
            session.WorkoutNameSnapshot,
            ToPerformedExerciseDtos(session.Exercises, nameById),
            snapshot);

    public static SessionStartResultDto ToSessionStartResultDto(
        WorkoutSession session,
        SessionSnapshotDto? snapshot) =>
        new(
            session.Id,
            session.Status,
            session.StartedAt,
            session.Source,
            snapshot);

    public static SessionSummaryDto ToSessionSummaryDto(
        WorkoutSession session,
        int totalSets,
        int totalExercises,
        string? traineeName = null) =>
        new(
            session.Id,
            session.TraineeId,
            traineeName,
            session.Source,
            session.Status,
            session.StartedAt,
            session.CompletedAt,
            session.DurationSeconds,
            totalSets,
            totalExercises,
            session.RpeOverall,
            session.PlanAssignmentId,
            session.WorkoutNameSnapshot);

    public static SessionListDto ToSessionListDto(
        IReadOnlyList<SessionSummaryDto> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(items, page, pageSize, totalCount);

    public static CompleteSessionResultDto ToCompleteSessionResultDto(
        WorkoutSession session,
        int totalSets,
        int totalExercises) =>
        new(
            session.Id,
            session.DurationSeconds,
            totalSets,
            totalExercises,
            session.CompletedAt!.Value);
}
