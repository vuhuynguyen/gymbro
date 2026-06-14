using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Shared.Time;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Mapping;

internal static class SessionMapping
{
    // Enums serialize as camelCase strings (matching the API wire format) but deserialize
    // case-insensitively with integer tolerance, so snapshots persisted before this format
    // (PascalCase `"Working"`) still load correctly.
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public static string SerializeSnapshot(SessionSnapshotDto snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    public static SessionSnapshotDto? DeserializeSnapshot(string? json) =>
        json != null ? JsonSerializer.Deserialize<SessionSnapshotDto>(json, SnapshotJsonOptions) : null;

    /// <summary>
    /// Trainee-facing redaction for <c>HideSetsReps</c> (filter-on-read). Drops the prescribed sets
    /// entirely so the trainee sees no prescribed set count, reps, weight, RPE or duration — they log
    /// their own sets, guided live by the coach. Exercise names/order are kept (needed to perform the
    /// work; <c>HideExercises</c> is a preview-only control). The stored snapshot is left intact, so a
    /// coach/admin still sees the full prescription.
    /// </summary>
    public static SessionSnapshotDto RedactSnapshotTargets(SessionSnapshotDto snapshot) =>
        new(
            snapshot.WorkoutName,
            snapshot.Exercises
                .Select(e => new SessionSnapshotExerciseDto(
                    e.PlanWorkoutExerciseId,
                    e.ExerciseId,
                    e.ExerciseName,
                    e.Order,
                    new List<SessionSnapshotSetDto>(),
                    e.SupersetGroupId))
                .ToList());

    /// <summary>
    /// Historical exercise names: prefers the name captured in the session snapshot at start
    /// over the current live name, so renaming/removing an exercise later does not relabel past logs.
    /// Exercises not in the snapshot (ad-hoc additions, substitutions) fall back to the live name.
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> MergeSnapshotNames(
        IReadOnlyDictionary<Guid, string> liveNames,
        SessionSnapshotDto? snapshot)
    {
        if (snapshot is null)
            return liveNames;

        var merged = new Dictionary<Guid, string>(liveNames);
        foreach (var exercise in snapshot.Exercises)
            if (!string.IsNullOrEmpty(exercise.ExerciseName))
                merged[exercise.ExerciseId] = exercise.ExerciseName;

        return merged;
    }

    public static IReadOnlyList<Guid> CollectExerciseIds(IEnumerable<PerformedExercise> exercises) =>
        exercises
            .SelectMany(e => new[] { e.ExerciseId, e.SubstitutedFromExerciseId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    public static PerformedSetDto ToPerformedSetDto(PerformedSet set, bool isPr = false) =>
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
            set.LoggedAt,
            isPr,
            set.Calories,
            set.AvgHeartRate,
            set.Rounds,
            set.ParentSetId);

    public static PerformedExerciseDto ToPerformedExerciseDto(
        PerformedExercise exercise,
        IReadOnlyDictionary<Guid, string>? nameById = null,
        IReadOnlySet<Guid>? prSetIds = null,
        IReadOnlyDictionary<Guid, LastPerformedSetDto>? lastPerformedByExercise = null)
    {
        nameById ??= new Dictionary<Guid, string>();

        return new PerformedExerciseDto(
            exercise.Id,
            exercise.ExerciseId,
            // Prefer the name captured at log time (durable history); fall back to live/snapshot for
            // rows created before name denormalization.
            exercise.ExerciseName ?? nameById.GetValueOrDefault(exercise.ExerciseId),
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
                .Select(s => ToPerformedSetDto(s, prSetIds?.Contains(s.Id) ?? false))
                .ToList(),
            exercise.TrackingType.ToString(),
            exercise.SupersetGroupId,
            lastPerformedByExercise?.GetValueOrDefault(exercise.ExerciseId));
    }

    public static IReadOnlyList<PerformedExerciseDto> ToPerformedExerciseDtos(
        IEnumerable<PerformedExercise> exercises,
        IReadOnlyDictionary<Guid, string> nameById,
        IReadOnlySet<Guid>? prSetIds = null,
        IReadOnlyDictionary<Guid, LastPerformedSetDto>? lastPerformedByExercise = null) =>
        exercises
            .OrderBy(e => e.Order)
            .Select(e => ToPerformedExerciseDto(e, nameById, prSetIds, lastPerformedByExercise))
            .ToList();

    /// <summary>Working-set volume: Σ (weight × reps) over working sets that carry both values.</summary>
    public static decimal ComputeVolumeKg(IEnumerable<PerformedExercise> exercises) =>
        exercises
            .SelectMany(e => e.Sets)
            .Where(s => s.SetType == PerformedSetType.Working && s.WeightKg.HasValue && s.Reps.HasValue)
            .Sum(s => s.WeightKg!.Value * s.Reps!.Value);

    /// <summary>
    /// 1-based plan week a session falls in, from the assignment start date and the session's LOCAL date — the
    /// date is resolved in the trainee's captured time-zone (<paramref name="ianaZone"/>, UTC fallback) so an
    /// evening session west of UTC isn't pushed into the next week. Null when the session isn't tied to a plan
    /// or starts before the assignment.
    /// </summary>
    public static int? ComputePlanWeek(DateOnly? startDate, DateTimeOffset sessionStart, string? ianaZone)
    {
        if (startDate is null) return null;
        var sessionDate = LocalDayResolver.LocalDateOf(sessionStart, ianaZone);
        var days = sessionDate.DayNumber - startDate.Value.DayNumber;
        if (days < 0) return null;
        return days / 7 + 1;
    }

    /// <summary>
    /// Detects per-exercise estimated-1RM PRs within a session: the top working set per lift whose e1RM
    /// strictly exceeds the trainee's prior best (from <paramref name="priorBestByExercise"/>).
    /// Returns the PR set ids plus a summary list ordered by e1RM gain.
    /// </summary>
    public static (IReadOnlySet<Guid> PrSetIds, IReadOnlyList<SessionPrDto> Prs) DetectPrs(
        IEnumerable<PerformedExercise> exercises,
        IReadOnlyDictionary<Guid, decimal> priorBestByExercise,
        IReadOnlyDictionary<Guid, string> nameById)
    {
        var prSetIds = new HashSet<Guid>();
        var prs = new List<SessionPrDto>();

        var byExercise = exercises
            .SelectMany(e => e.Sets.Select(s => (e.ExerciseId, Set: s)))
            .Where(x => x.Set.SetType == PerformedSetType.Working
                && x.Set.EstimatedOneRepMaxKg.HasValue
                && x.Set.WeightKg.HasValue
                && x.Set.Reps.HasValue)
            .GroupBy(x => x.ExerciseId);

        foreach (var group in byExercise)
        {
            var topSet = group
                .OrderByDescending(x => x.Set.EstimatedOneRepMaxKg!.Value)
                .First()
                .Set;

            var prior = priorBestByExercise.TryGetValue(group.Key, out var best) ? best : (decimal?)null;
            if (prior.HasValue && topSet.EstimatedOneRepMaxKg!.Value <= prior.Value)
                continue;

            prSetIds.Add(topSet.Id);
            prs.Add(new SessionPrDto(
                group.Key,
                nameById.GetValueOrDefault(group.Key),
                topSet.WeightKg!.Value,
                topSet.Reps!.Value,
                topSet.EstimatedOneRepMaxKg!.Value,
                prior));
        }

        return (prSetIds, prs
            .OrderByDescending(p => p.EstimatedOneRepMaxKg - (p.PreviousEstimatedOneRepMaxKg ?? 0))
            .ToList());
    }

    // The stored snapshot is always complete (full prescribed sets/targets); the trainee's view is
    // redacted on read via RedactSnapshotTargets so coach analytics/history stay intact (filter-on-read,
    // see BUSINESS_RULES.md "Visibility modes").
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
                        s.SetType,
                        s.TargetReps,
                        s.TargetWeightKg,
                        s.TargetRpe,
                        s.TargetDurationSeconds,
                        s.RestSeconds,
                        s.TargetDistanceM,
                        s.TargetRounds))
                    .ToList(),
                e.SupersetGroupId))
            .ToList();

        return new SessionSnapshotDto(workout.Name, exercises);
    }

    public static ActiveSessionDto ToActiveSessionDto(
        WorkoutSession session,
        SessionSnapshotDto? snapshot,
        IReadOnlyDictionary<Guid, string> nameById,
        IReadOnlyDictionary<Guid, LastPerformedSetDto>? lastPerformedByExercise = null) =>
        new(
            session.Id,
            session.Status,
            session.StartedAt,
            session.Source,
            snapshot,
            ToPerformedExerciseDtos(session.Exercises, nameById, lastPerformedByExercise: lastPerformedByExercise));

    public static SessionDetailDto ToSessionDetailDto(
        WorkoutSession session,
        SessionSnapshotDto? snapshot,
        IReadOnlyDictionary<Guid, string> nameById,
        IReadOnlySet<Guid>? prSetIds = null,
        IReadOnlyList<SessionPrDto>? prs = null,
        string? programName = null,
        int? planWeek = null) =>
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
            ToPerformedExerciseDtos(session.Exercises, nameById, prSetIds),
            snapshot,
            ComputeVolumeKg(session.Exercises),
            programName,
            planWeek,
            prs ?? []);

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
        decimal totalVolumeKg = 0m,
        int prCount = 0,
        string? programName = null,
        int? planWeek = null,
        int? weeklyGoal = null,
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
            session.WorkoutNameSnapshot,
            totalVolumeKg,
            prCount,
            programName,
            planWeek,
            weeklyGoal,
            session.ClientTimezone);

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
