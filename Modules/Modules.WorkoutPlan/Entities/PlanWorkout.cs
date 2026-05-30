using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutPlanModule.Entities;

/// <summary>A numbered training day in a plan (not tied to calendar).</summary>
public sealed class PlanWorkout : BaseEntity, ITenantEntity
{
    public Guid WorkoutPlanId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = null!;

    private readonly List<PlanWorkoutExercise> _exercises = new();
    public IReadOnlyCollection<PlanWorkoutExercise> Exercises => _exercises;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanWorkout() { }

    public static PlanWorkout Create(Guid workoutPlanId, Guid tenantId, string name, int order)
    {
        if (workoutPlanId == Guid.Empty)
            throw new ArgumentException("WorkoutPlanId is required.", nameof(workoutPlanId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (order < 1)
            throw new ArgumentOutOfRangeException(nameof(order));

        return new PlanWorkout
        {
            Id = Guid.NewGuid(),
            WorkoutPlanId = workoutPlanId,
            TenantId = tenantId,
            Name = name.Trim(),
            Order = order
        };
    }

    internal void AddExercise(Guid tenantId, Guid exerciseId, int order, IReadOnlyList<PlanWorkoutSetData> sets)
    {
        var exercise = PlanWorkoutExercise.Create(Id, tenantId, exerciseId, order);
        foreach (var set in sets.OrderBy(s => s.Order))
            exercise.AddSet(tenantId, set);
        _exercises.Add(exercise);
    }
}
