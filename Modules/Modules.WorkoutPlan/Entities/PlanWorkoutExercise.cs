using BuildingBlocks.Shared.DomainPrimitives;

using Modules.WorkoutPlanModule.Application;
namespace Modules.WorkoutPlanModule.Entities;

public sealed record PlanWorkoutSetData(
    PlanSetType SetType,
    int? TargetReps,
    decimal? TargetWeightKg,
    int? TargetRpe,
    int? TargetDurationSeconds,
    int RestSeconds,
    int Order,
    int? TargetDistanceM = null,
    int? TargetRounds = null);

public sealed class PlanWorkoutExercise : BaseEntity, ITenantEntity
{
    public Guid PlanWorkoutId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Order { get; private set; }
    /// <summary>Exercises in a workout that share a non-null group id are prescribed as a superset.</summary>
    public Guid? SupersetGroupId { get; private set; }

    private readonly List<PlanWorkoutExerciseSet> _prescribedSets = new();
    public IReadOnlyCollection<PlanWorkoutExerciseSet> PrescribedSets => _prescribedSets;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanWorkoutExercise() { }

    public static PlanWorkoutExercise Create(
        Guid planWorkoutId,
        Guid tenantId,
        Guid exerciseId,
        int order,
        Guid? supersetGroupId = null)
    {
        if (planWorkoutId == Guid.Empty)
            throw new DomainException("PlanWorkoutId is required.");
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId is required.");
        if (exerciseId == Guid.Empty)
            throw new DomainException("ExerciseId is required.");
        if (order < 1)
            throw new DomainException("order is out of range.");

        return new PlanWorkoutExercise
        {
            Id = Guid.NewGuid(),
            PlanWorkoutId = planWorkoutId,
            TenantId = tenantId,
            ExerciseId = exerciseId,
            Order = order,
            SupersetGroupId = supersetGroupId
        };
    }

    internal void AddSet(Guid tenantId, PlanWorkoutSetData set)
    {
        _prescribedSets.Add(PlanWorkoutExerciseSet.Create(
            Id,
            tenantId,
            set.Order,
            set.SetType,
            set.TargetReps,
            set.TargetWeightKg,
            set.TargetRpe,
            set.TargetDurationSeconds,
            set.RestSeconds,
            set.TargetDistanceM,
            set.TargetRounds));
    }
}
