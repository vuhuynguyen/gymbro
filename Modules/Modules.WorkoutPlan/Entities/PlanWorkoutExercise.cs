using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutPlanModule.Entities;

public sealed record PlanWorkoutSetData(
    PlanSetType SetType,
    int? TargetReps,
    decimal? TargetWeightKg,
    int? TargetRpe,
    int? TargetDurationSeconds,
    int RestSeconds,
    int Order);

public sealed class PlanWorkoutExercise : BaseEntity, ITenantEntity
{
    public Guid PlanWorkoutId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Order { get; private set; }

    private readonly List<PlanWorkoutExerciseSet> _prescribedSets = new();
    public IReadOnlyCollection<PlanWorkoutExerciseSet> PrescribedSets => _prescribedSets;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanWorkoutExercise() { }

    public static PlanWorkoutExercise Create(
        Guid planWorkoutId,
        Guid tenantId,
        Guid exerciseId,
        int order)
    {
        if (planWorkoutId == Guid.Empty)
            throw new ArgumentException("PlanWorkoutId is required.", nameof(planWorkoutId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("ExerciseId is required.", nameof(exerciseId));
        if (order < 1)
            throw new ArgumentOutOfRangeException(nameof(order));

        return new PlanWorkoutExercise
        {
            Id = Guid.NewGuid(),
            PlanWorkoutId = planWorkoutId,
            TenantId = tenantId,
            ExerciseId = exerciseId,
            Order = order
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
            set.RestSeconds));
    }
}
