using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class PerformedExercise : BaseEntity, ITenantEntity
{
    public Guid SessionId { get; private set; }
    public Guid ExerciseId { get; private set; }
    /// <summary>Exercise name captured at log/substitute time so the log survives later renames/deletes.</summary>
    public string? ExerciseName { get; private set; }
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
        int order,
        string? exerciseName)
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

    public void Substitute(Guid substituteExerciseId, string? substituteExerciseName, string? notes)
    {
        SubstitutedFromExerciseId = ExerciseId;
        ExerciseId = substituteExerciseId;
        ExerciseName = string.IsNullOrWhiteSpace(substituteExerciseName) ? null : substituteExerciseName.Trim();
        Status = ExercisePerformStatus.Substituted;
        Notes = notes;
    }

    public void MarkCompleted()
    {
        if (Status == ExercisePerformStatus.InProgress)
            Status = ExercisePerformStatus.Completed;
    }
}
