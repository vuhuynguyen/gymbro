using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class PerformedExercise : BaseEntity, ITenantEntity
{
    public Guid SessionId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? PlanWorkoutExerciseId { get; private set; }
    public Guid? SubstitutedFromExerciseId { get; private set; }
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
        int order)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("SessionId is required.", nameof(sessionId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (exerciseId == Guid.Empty) throw new ArgumentException("ExerciseId is required.", nameof(exerciseId));

        return new PerformedExercise
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            ExerciseId = exerciseId,
            PlanWorkoutExerciseId = planWorkoutExerciseId,
            Order = order,
            Status = ExercisePerformStatus.InProgress
        };
    }

    public void Skip(string? notes)
    {
        Status = ExercisePerformStatus.Skipped;
        Notes = notes;
    }

    public void Substitute(Guid substituteExerciseId, string? notes)
    {
        SubstitutedFromExerciseId = ExerciseId;
        ExerciseId = substituteExerciseId;
        Status = ExercisePerformStatus.Substituted;
        Notes = notes;
    }

    public void MarkCompleted()
    {
        if (Status == ExercisePerformStatus.InProgress)
            Status = ExercisePerformStatus.Completed;
    }
}
