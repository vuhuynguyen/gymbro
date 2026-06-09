using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutSessionModule.Entities;

public sealed class PerformedSet : BaseEntity, ITenantEntity
{
    public Guid PerformedExerciseId { get; private set; }
    public Guid? PlanSetId { get; private set; }
    /// <summary>
    /// When set, this row is a stage of a drop/rest-pause set whose lead set is <see cref="ParentSetId"/> — the
    /// cluster counts as ONE logical set (only parentless rows are counted), while each stage still adds to volume.
    /// One level only: a stage never has its own children.
    /// </summary>
    public Guid? ParentSetId { get; private set; }
    public int SetNumber { get; private set; }
    public PerformedSetType SetType { get; private set; }
    public int? Reps { get; private set; }
    public decimal? WeightKg { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int? DistanceM { get; private set; }
    /// <summary>Energy expended for this set, for cardio/HIIT logging.</summary>
    public int? Calories { get; private set; }
    /// <summary>Average heart rate (bpm) for this set, for cardio/HIIT logging.</summary>
    public int? AvgHeartRate { get; private set; }
    /// <summary>Number of rounds/intervals completed, for HIIT/circuit logging.</summary>
    public int? Rounds { get; private set; }
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
        bool isCompleted,
        int? calories = null,
        int? avgHeartRate = null,
        int? rounds = null,
        Guid? parentSetId = null)
    {
        if (performedExerciseId == Guid.Empty)
            throw new DomainException("PerformedExerciseId is required.");
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId is required.");

        return new PerformedSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PerformedExerciseId = performedExerciseId,
            PlanSetId = planSetId,
            ParentSetId = parentSetId,
            SetNumber = setNumber,
            SetType = setType,
            Reps = reps,
            WeightKg = weightKg,
            DurationSeconds = durationSeconds,
            DistanceM = distanceM,
            Calories = calories,
            AvgHeartRate = avgHeartRate,
            Rounds = rounds,
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
        PerformedSetType? setType,
        int? calories = null,
        int? avgHeartRate = null,
        int? rounds = null)
    {
        if (reps.HasValue) Reps = reps;
        if (weightKg.HasValue) WeightKg = weightKg;
        if (durationSeconds.HasValue) DurationSeconds = durationSeconds;
        if (distanceM.HasValue) DistanceM = distanceM;
        if (calories.HasValue) Calories = calories;
        if (avgHeartRate.HasValue) AvgHeartRate = avgHeartRate;
        if (rounds.HasValue) Rounds = rounds;
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
