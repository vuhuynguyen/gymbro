using BuildingBlocks.Shared.DomainPrimitives;
using BuildingBlocks.Shared.Tracking;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class PerformedExercise : BaseEntity, ITenantEntity
{
    public Guid SessionId { get; private set; }
    public Guid ExerciseId { get; private set; }
    /// <summary>Exercise name captured at log/substitute time so the log survives later renames/deletes.</summary>
    public string? ExerciseName { get; private set; }
    /// <summary>
    /// Logging mode captured at add/substitute time (durable history + lets the loggers and the per-mode set
    /// validation work without a per-log cross-module lookup). Defaults to Strength for rows logged before this existed.
    /// </summary>
    public ExerciseTrackingType TrackingType { get; private set; } = ExerciseTrackingType.Strength;
    public Guid? PlanWorkoutExerciseId { get; private set; }
    public Guid? SubstitutedFromExerciseId { get; private set; }
    /// <summary>Exercises in a session that share a non-null group id are performed as a superset (rotated, rest after the round).</summary>
    public Guid? SupersetGroupId { get; private set; }
    public int Order { get; private set; }
    public ExercisePerformStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<PerformedSet> _sets = new();
    public IReadOnlyCollection<PerformedSet> Sets => _sets;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PerformedExercise() { }

    public static PerformedExercise Create(
        Guid sessionId,
        Guid tenantId,
        Guid exerciseId,
        Guid? planWorkoutExerciseId,
        int order,
        string? exerciseName,
        ExerciseTrackingType trackingType = ExerciseTrackingType.Strength,
        Guid? supersetGroupId = null)
    {
        if (sessionId == Guid.Empty) throw new DomainException("SessionId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        if (exerciseId == Guid.Empty) throw new DomainException("ExerciseId is required.");

        return new PerformedExercise
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            ExerciseId = exerciseId,
            ExerciseName = string.IsNullOrWhiteSpace(exerciseName) ? null : exerciseName.Trim(),
            TrackingType = trackingType,
            PlanWorkoutExerciseId = planWorkoutExerciseId,
            SupersetGroupId = supersetGroupId,
            Order = order,
            Status = ExercisePerformStatus.InProgress
        };
    }

    public void Skip(string? notes)
    {
        Status = ExercisePerformStatus.Skipped;
        Notes = notes;
    }

    public void Substitute(
        Guid substituteExerciseId,
        string? substituteExerciseName,
        string? notes,
        ExerciseTrackingType substituteTrackingType = ExerciseTrackingType.Strength)
    {
        SubstitutedFromExerciseId = ExerciseId;
        ExerciseId = substituteExerciseId;
        ExerciseName = string.IsNullOrWhiteSpace(substituteExerciseName) ? null : substituteExerciseName.Trim();
        TrackingType = substituteTrackingType;
        Status = ExercisePerformStatus.Substituted;
        Notes = notes;
    }

    /// <summary>Join a superset group (or leave it, when null). Members of a shared non-null group rotate together, resting after the round.</summary>
    public void SetSupersetGroup(Guid? supersetGroupId)
    {
        SupersetGroupId = supersetGroupId;
    }

    public void MarkCompleted()
    {
        if (Status == ExercisePerformStatus.InProgress)
            Status = ExercisePerformStatus.Completed;
    }
}
