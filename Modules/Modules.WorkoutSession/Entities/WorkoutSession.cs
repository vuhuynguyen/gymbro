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

    /// <summary>
    /// Number of exercises in this session that set a new e1RM personal record versus the trainee's
    /// prior history. Finalized once, when the session reaches a terminal state (see <see cref="Complete"/>);
    /// read directly by the session list so it never re-walks full history per page. 0 while in progress.
    /// </summary>
    public int PrCount { get; private set; }

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
        if (traineeId == Guid.Empty) throw new DomainException("TraineeId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");

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

    /// <summary>
    /// Pre-populates the session with the planned workout's exercises (in order) so the trainee sees
    /// the plan to perform immediately. Called once at start for plan-based sessions; targets/sets are
    /// resolved from the stored snapshot on read.
    /// </summary>
    public void SeedPlannedExercises(
        IEnumerable<(Guid ExerciseId, Guid? PlanWorkoutExerciseId, int Order, string? ExerciseName)> planned)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");

        foreach (var p in planned.OrderBy(x => x.Order))
            _exercises.Add(PerformedExercise.Create(
                Id, tenantId, p.ExerciseId, p.PlanWorkoutExerciseId, p.Order, p.ExerciseName));
    }

    public void Complete(int? rpeOverall, string? notes, DateTimeOffset? completedAt, int prCount)
    {
        if (Status != SessionStatus.InProgress)
            throw new DomainException("Only in-progress sessions can be completed.");
        if (prCount < 0)
            throw new DomainException("prCount is out of range.");

        var now = completedAt ?? DateTimeOffset.UtcNow;
        Status = SessionStatus.Completed;
        CompletedAt = now;
        DurationSeconds = (int)(now - StartedAt).TotalSeconds;
        RpeOverall = rpeOverall;
        Notes = notes;
        PrCount = prCount;

        RaiseDomainEvent(new SessionCompletedEvent(Id, TraineeId, TenantId!.Value, DateTimeOffset.UtcNow));
    }

    public void Abandon(string? notes)
    {
        if (Status != SessionStatus.InProgress)
            throw new DomainException("Only in-progress sessions can be abandoned.");

        Status = SessionStatus.Abandoned;
        CompletedAt = DateTimeOffset.UtcNow;
        Notes = notes;
    }
}
