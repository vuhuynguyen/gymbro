using BuildingBlocks.Shared.DomainPrimitives;

using Modules.WorkoutPlanModule.Application;
namespace Modules.WorkoutPlanModule.Entities;

public sealed class PlanWorkoutExerciseSet : BaseEntity, ITenantEntity
{
    public Guid PlanWorkoutExerciseId { get; private set; }
    public int Order { get; private set; }
    public PlanSetType SetType { get; private set; }
    public int? TargetReps { get; private set; }
    public decimal? TargetWeightKg { get; private set; }
    public int? TargetRpe { get; private set; }
    public int? TargetDurationSeconds { get; private set; }
    /// <summary>Prescribed distance (m), for cardio plans.</summary>
    public int? TargetDistanceM { get; private set; }
    /// <summary>Prescribed rounds/intervals, for HIIT/circuit plans.</summary>
    public int? TargetRounds { get; private set; }
    public int RestSeconds { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanWorkoutExerciseSet() { }

    internal static PlanWorkoutExerciseSet Create(
        Guid planWorkoutExerciseId,
        Guid tenantId,
        int order,
        PlanSetType setType,
        int? targetReps,
        decimal? targetWeightKg,
        int? targetRpe,
        int? targetDurationSeconds,
        int restSeconds,
        int? targetDistanceM = null,
        int? targetRounds = null)
    {
        if (planWorkoutExerciseId == Guid.Empty)
            throw new DomainException("PlanWorkoutExerciseId is required.");
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId is required.");
        if (order < 1)
            throw new DomainException("order is out of range.");
        if (restSeconds < 0)
            throw new DomainException("restSeconds is out of range.");

        return new PlanWorkoutExerciseSet
        {
            Id = Guid.NewGuid(),
            PlanWorkoutExerciseId = planWorkoutExerciseId,
            TenantId = tenantId,
            Order = order,
            SetType = setType,
            TargetReps = targetReps,
            TargetWeightKg = targetWeightKg,
            TargetRpe = targetRpe,
            TargetDurationSeconds = targetDurationSeconds,
            TargetDistanceM = targetDistanceM,
            TargetRounds = targetRounds,
            RestSeconds = restSeconds
        };
    }
}
