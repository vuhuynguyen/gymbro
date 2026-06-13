using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.WorkoutPlanModule.Entities;

/// <summary>
/// Tenant-scoped workout plan template (sequence-based days, not calendar-bound). Authoring is draft-first: a
/// single mutable <b>draft head</b> absorbs every edit (the draft is replaced in place, not version-bumped), and
/// only <see cref="Publish"/> turns a draft into an immutable published version — the only thing that advances the
/// version trainees and assignments see.
/// </summary>
public sealed class WorkoutPlan : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int? DurationWeeks { get; private set; }
    public int? WorkoutsPerWeek { get; private set; }

    /// <summary>
    /// Unpublished working copy. Edits land on the draft head without bumping the version; published versions
    /// are immutable. A draft is excluded from the (TemplateId, Version) uniqueness rule, never assignable, and
    /// invisible to trainees until <see cref="Publish"/> flips it to published.
    /// </summary>
    public bool IsDraft { get; private set; }

    /// <summary>Retired template: hidden from the active plan list, not editable, not assignable. Reversible.</summary>
    public bool IsArchived { get; private set; }

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
            throw new DomainException("TenantId is required.");
        if (createdBy == Guid.Empty)
            throw new DomainException("CreatedBy is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");

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
            IsDraft = true,
            IsDeleted = false
        };
    }

    /// <summary>Replaces all plan workouts and their exercises (plan builder save).</summary>
    public void ReplaceStructure(
        IReadOnlyList<(string Name, int Order, IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets, Guid? SupersetGroupId)> Exercises)> workouts)
    {
        ArgumentNullException.ThrowIfNull(workouts);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");

        _workouts.Clear();

        foreach (var w in workouts.OrderBy(x => x.Order))
        {
            var planWorkout = PlanWorkout.Create(Id, tenantId, w.Name, w.Order);
            foreach (var ex in w.Exercises.OrderBy(e => e.Order))
                planWorkout.AddExercise(tenantId, ex.ExerciseId, ex.Order, ex.Sets, ex.SupersetGroupId);

            _workouts.Add(planWorkout);
        }
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
    }

    public void SetArchived(bool archived)
    {
        IsArchived = archived;
    }

    /// <summary>Promotes this draft head to an immutable published version. Throws if already published.</summary>
    public void Publish()
    {
        if (!IsDraft)
            throw new DomainException("Plan is already published.");
        IsDraft = false;
    }

    /// <summary>
    /// Deep-copies a source version into a fresh <b>draft</b> row at <paramref name="version"/> (same TemplateId,
    /// new Id, IsDraft = true). The caller decides the version: keep the source's number when replacing an existing
    /// draft head, or source + 1 when forking a new draft off a published version. Built as an untracked graph so
    /// it persists via a single <c>AddAsync</c> (no in-place child mutation).
    /// </summary>
    public static WorkoutPlan CreateDraft(
        WorkoutPlan current,
        Guid createdBy,
        int version,
        string name,
        string? description,
        int? durationWeeks,
        int? workoutsPerWeek)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (createdBy == Guid.Empty)
            throw new DomainException("CreatedBy is required.");
        if (version < 1)
            throw new DomainException("version is out of range.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");

        var next = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = current.TemplateId,
            TenantId = current.TenantId,
            CreatedBy = createdBy,
            Version = version,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DurationWeeks = durationWeeks,
            WorkoutsPerWeek = workoutsPerWeek,
            IsDraft = true,
            IsDeleted = false
        };

        var copied = current.Workouts
            .OrderBy(w => w.Order)
            .Select(w => (
                w.Name,
                w.Order,
                (IReadOnlyList<(Guid ExerciseId, int Order, IReadOnlyList<PlanWorkoutSetData> Sets, Guid? SupersetGroupId)>)w.Exercises
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
                                s.Order,
                                s.TargetDistanceM,
                                s.TargetRounds))
                            .ToList(),
                        e.SupersetGroupId))
                    .ToList()))
            .ToList();

        next.ReplaceStructure(copied);
        return next;
    }
}
