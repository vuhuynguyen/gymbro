using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class PerformedSet : BaseEntity, ITenantEntity
{
    public Guid PerformedExerciseId { get; private set; }
    public Guid? PlanSetId { get; private set; }
    public int SetNumber { get; private set; }
    public PerformedSetType SetType { get; private set; }
    public int? Reps { get; private set; }
    public decimal? WeightKg { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int? DistanceM { get; private set; }
    public int? Rpe { get; private set; }
    public int? RestSeconds { get; private set; }
    public bool IsCompleted { get; private set; }
    public decimal? EstimatedOneRepMaxKg { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PerformedSet() { }

    public static PerformedSet Log(
        Guid performedExerciseId,
        Guid tenantId,
        Guid? planSetId,
        int setNumber,
        PerformedSetType setType,
        int? reps,
        decimal? weightKg,
        int? durationSeconds,
        int? distanceM,
        int? rpe,
        int? restSeconds,
        bool isCompleted)
    {
        if (performedExerciseId == Guid.Empty)
            throw new ArgumentException("PerformedExerciseId is required.", nameof(performedExerciseId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return new PerformedSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PerformedExerciseId = performedExerciseId,
            PlanSetId = planSetId,
            SetNumber = setNumber,
            SetType = setType,
            Reps = reps,
            WeightKg = weightKg,
            DurationSeconds = durationSeconds,
            DistanceM = distanceM,
            Rpe = rpe,
            RestSeconds = restSeconds,
            IsCompleted = isCompleted,
            EstimatedOneRepMaxKg = ComputeOneRepMax(setType, reps, weightKg),
            LoggedAt = DateTimeOffset.UtcNow
        };
    }

    public void Edit(
        int? reps,
        decimal? weightKg,
        int? durationSeconds,
        int? distanceM,
        int? rpe,
        int? restSeconds,
        bool? isCompleted,
        PerformedSetType? setType)
    {
        if (reps.HasValue) Reps = reps;
        if (weightKg.HasValue) WeightKg = weightKg;
        if (durationSeconds.HasValue) DurationSeconds = durationSeconds;
        if (distanceM.HasValue) DistanceM = distanceM;
        if (rpe.HasValue) Rpe = rpe;
        if (restSeconds.HasValue) RestSeconds = restSeconds;
        if (isCompleted.HasValue) IsCompleted = isCompleted.Value;
        if (setType.HasValue) SetType = setType.Value;

        EstimatedOneRepMaxKg = ComputeOneRepMax(SetType, Reps, WeightKg);
    }

    private static decimal? ComputeOneRepMax(PerformedSetType setType, int? reps, decimal? weightKg)
    {
        if (setType != PerformedSetType.Working || !reps.HasValue || !weightKg.HasValue || reps.Value <= 0)
            return null;

        return Math.Round(weightKg.Value * (1m + reps.Value / 30m), 1);
    }
}
