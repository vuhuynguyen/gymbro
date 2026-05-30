using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class WorkoutSession : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TraineeId { get; private set; }
    public SessionSource Source { get; private set; }
    public SessionStatus Status { get; private set; }
    public Guid? PlanAssignmentId { get; private set; }
    public Guid? PlannedWorkoutId { get; private set; }
    public string? WorkoutNameSnapshot { get; private set; }
    public string? SnapshotJson { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int? RpeOverall { get; private set; }
    public decimal? BodyweightKg { get; private set; }
    public string? Notes { get; private set; }
    public string? ClientTimezone { get; private set; }

    private readonly List<PerformedExercise> _exercises = new();
    public IReadOnlyCollection<PerformedExercise> Exercises => _exercises;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private WorkoutSession() { }

    public static WorkoutSession Start(
        Guid traineeId,
        Guid tenantId,
        SessionSource source,
        Guid? planAssignmentId,
        Guid? plannedWorkoutId,
        string? workoutNameSnapshot,
        string? snapshotJson,
        string? clientTimezone,
        decimal? bodyweightKg)
    {
        if (traineeId == Guid.Empty) throw new ArgumentException("TraineeId is required.", nameof(traineeId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return new WorkoutSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TraineeId = traineeId,
            Source = source,
            Status = SessionStatus.InProgress,
            PlanAssignmentId = planAssignmentId,
            PlannedWorkoutId = plannedWorkoutId,
            WorkoutNameSnapshot = workoutNameSnapshot,
            SnapshotJson = snapshotJson,
            ClientTimezone = clientTimezone,
            BodyweightKg = bodyweightKg,
            StartedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
    }

    public void Complete(int? rpeOverall, string? notes, DateTimeOffset? completedAt)
    {
        if (Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Only in-progress sessions can be completed.");

        var now = completedAt ?? DateTimeOffset.UtcNow;
        Status = SessionStatus.Completed;
        CompletedAt = now;
        DurationSeconds = (int)(now - StartedAt).TotalSeconds;
        RpeOverall = rpeOverall;
        Notes = notes;

        RaiseDomainEvent(new SessionCompletedEvent(Id, TraineeId, TenantId!.Value, DateTimeOffset.UtcNow));
    }

    public void Abandon(string? notes)
    {
        if (Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Only in-progress sessions can be abandoned.");

        Status = SessionStatus.Abandoned;
        CompletedAt = DateTimeOffset.UtcNow;
        Notes = notes;
    }
}
