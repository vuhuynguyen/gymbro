using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutPlanModule.Entities;

/// <summary>
/// Tenant-scoped workout plan template (sequence-based days, not calendar-bound).
/// </summary>
public sealed class WorkoutPlan : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int? DurationWeeks { get; private set; }
    public int? WorkoutsPerWeek { get; private set; }

    private readonly List<PlanWorkout> _workouts = new();
    public IReadOnlyCollection<PlanWorkout> Workouts => _workouts;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private WorkoutPlan() { }

    public static WorkoutPlan Create(
        Guid tenantId,
        Guid createdBy,
        string name,
        string? description,
        int? durationWeeks,
        int? workoutsPerWeek)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy is required.", nameof(createdBy));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = createdBy,
            Version = 1,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DurationWeeks = durationWeeks,
            WorkoutsPerWeek = workoutsPerWeek,
            IsDeleted = false
        };
    }

    public void UpdateMetadata(string name, string? description, int? durationWeeks, int? workoutsPerWeek)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DurationWeeks = durationWeeks;
        WorkoutsPerWeek = workoutsPerWeek;
    }

    /// <summary>Replaces all plan workouts and their exercises (plan builder save).</summary>
    public void ReplaceStructure(
        IReadOnlyList<(string Name, int Order, IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets)> Exercises)> workouts)
    {
        ArgumentNullException.ThrowIfNull(workouts);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");

        _workouts.Clear();

        foreach (var w in workouts.OrderBy(x => x.Order))
        {
            var planWorkout = PlanWorkout.Create(Id, tenantId, w.Name, w.Order);
            foreach (var ex in w.Exercises.OrderBy(e => e.Order))
                planWorkout.AddExercise(tenantId, ex.ExerciseId, ex.Order, ex.Sets);

            _workouts.Add(planWorkout);
        }
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
    }

    public static WorkoutPlan CreateNewVersion(
        WorkoutPlan current,
        Guid createdBy,
        string name,
        string? description,
        int? durationWeeks,
        int? workoutsPerWeek)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy is required.", nameof(createdBy));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var next = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = current.TemplateId,
            TenantId = current.TenantId,
            CreatedBy = createdBy,
            Version = current.Version + 1,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DurationWeeks = durationWeeks,
            WorkoutsPerWeek = workoutsPerWeek,
            IsDeleted = false
        };

        var copied = current.Workouts
            .OrderBy(w => w.Order)
            .Select(w => (
                w.Name,
                w.Order,
                (IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets)>)w.Exercises
                    .OrderBy(e => e.Order)
                    .Select(e => (
                        e.ExerciseId,
                        e.Order,
                        (IReadOnlyList<PlanWorkoutSetData>)e.PrescribedSets
                            .OrderBy(s => s.Order)
                            .Select(s => new PlanWorkoutSetData(
                                s.SetType,
                                s.TargetReps,
                                s.TargetWeightKg,
                                s.TargetRpe,
                                s.TargetDurationSeconds,
                                s.RestSeconds,
                                s.Order))
                            .ToList()))
                    .ToList()))
            .ToList();

        next.ReplaceStructure(copied);
        return next;
    }
}
