using BuildingBlocks.Shared.DomainPrimitives;
using BuildingBlocks.Shared.Tracking;

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
        IEnumerable<(Guid ExerciseId, Guid? PlanWorkoutExerciseId, int Order, string? ExerciseName, ExerciseTrackingType TrackingType, Guid? SupersetGroupId)> planned)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");

        foreach (var p in planned.OrderBy(x => x.Order))
            _exercises.Add(PerformedExercise.Create(
                Id, tenantId, p.ExerciseId, p.PlanWorkoutExerciseId, p.Order, p.ExerciseName, p.TrackingType, p.SupersetGroupId));
    }

    public void Complete(int? rpeOverall, string? notes, DateTimeOffset? completedAt, int prCount)
    {
        if (Status != SessionStatus.InProgress)
            throw new DomainException("Only in-progress sessions can be completed.");
        if (prCount < 0)
            throw new DomainException("prCount is out of range.");

        // DurationSeconds is a server-derived fact. A client-supplied CompletedAt that predates the start
        // would yield a negative duration — clamp it to the server clock (entity-level backstop). A
        // future CompletedAt is rejected earlier by CompleteSessionCommandValidator. (Audit finding 6.)
        var serverNow = DateTimeOffset.UtcNow;
        var now = completedAt ?? serverNow;
        if (now < StartedAt)
            now = serverNow;
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

        var now = DateTimeOffset.UtcNow;
        Status = SessionStatus.Abandoned;
        CompletedAt = now;
        // Record elapsed time even when abandoned (parity with Complete) so the history row / detail
        // can show how long the session ran before it was given up.
        DurationSeconds = (int)(now - StartedAt).TotalSeconds;
        Notes = notes;
    }
}
