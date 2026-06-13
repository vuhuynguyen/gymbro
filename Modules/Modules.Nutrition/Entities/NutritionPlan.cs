using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Tenant-scoped nutrition plan template — a day's worth of prescribed meals + supplements. Versions share a
/// <c>TemplateId</c> with an incrementing <c>Version</c>. Authoring is draft-first: a single mutable <b>draft
/// head</b> absorbs every edit (the draft is replaced in place, not version-bumped), and only <see cref="Publish"/>
/// turns a draft into an immutable published version — the only thing that advances the version trainees and
/// assignments see. Direct port of the <c>WorkoutPlan</c> lifecycle.
/// </summary>
public sealed class NutritionPlan : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>
    /// Unpublished working copy. Edits land on the draft head without bumping the version; published versions
    /// are immutable. A draft is excluded from the (TemplateId, Version) uniqueness rule, never assignable, and
    /// invisible to trainees until <see cref="Publish"/> flips it to published.
    /// </summary>
    public bool IsDraft { get; private set; }

    /// <summary>Retired template: hidden from the active plan list, not editable, not assignable. Reversible.</summary>
    public bool IsArchived { get; private set; }

    private readonly List<PlanMeal> _meals = new();
    public IReadOnlyCollection<PlanMeal> Meals => _meals;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private NutritionPlan() { }

    public static NutritionPlan Create(Guid tenantId, Guid createdBy, string name, string? description)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        if (createdBy == Guid.Empty) throw new DomainException("CreatedBy is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");

        return new NutritionPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = createdBy,
            Version = 1,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsDraft = true,
            IsDeleted = false
        };
    }

    /// <summary>Replaces all meals and their items (plan-builder save). Order preserved.</summary>
    public void ReplaceStructure(IReadOnlyList<PlanMealData> meals)
    {
        ArgumentNullException.ThrowIfNull(meals);
        var tenantId = TenantId ?? throw new InvalidOperationException("TenantId is not set.");

        _meals.Clear();
        foreach (var m in meals.OrderBy(x => x.Order))
        {
            var meal = PlanMeal.Create(Id, tenantId, m.Name, m.Order, m.ScheduledTime, m.DayApplicability);
            foreach (var item in m.Items.OrderBy(i => i.Order))
                meal.AddItem(tenantId, item);
            _meals.Add(meal);
        }
    }

    public void MarkDeleted() => IsDeleted = true;

    public void SetArchived(bool archived) => IsArchived = archived;

    /// <summary>Promotes this draft head to an immutable published version. No-op-safe: throws if already published.</summary>
    public void Publish()
    {
        if (!IsDraft) throw new DomainException("Plan is already published.");
        IsDraft = false;
    }

    /// <summary>
    /// Deep-copies a source version into a fresh <b>draft</b> row at <paramref name="version"/> (same TemplateId,
    /// new Id, IsDraft = true). The caller decides the version: keep the source's number when replacing an existing
    /// draft head, or source + 1 when forking a new draft off a published version. Built as an untracked graph so
    /// it persists via a single <c>AddAsync</c> (no in-place child mutation).
    /// </summary>
    public static NutritionPlan CreateDraft(
        NutritionPlan current,
        Guid createdBy,
        int version,
        string name,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (createdBy == Guid.Empty) throw new DomainException("CreatedBy is required.");
        if (version < 1) throw new DomainException("version is out of range.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");

        var next = new NutritionPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = current.TemplateId,
            TenantId = current.TenantId,
            CreatedBy = createdBy,
            Version = version,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsDraft = true,
            IsDeleted = false
        };

        var copied = current.Meals
            .OrderBy(m => m.Order)
            .Select(m => new PlanMealData(
                m.Name,
                m.Order,
                m.ScheduledTime,
                m.DayApplicability,
                m.Items
                    .OrderBy(i => i.Order)
                    .Select(i => new PlanMealItemData(
                        i.FoodId, i.Order, i.Quantity, i.FoodNameSnapshot, i.ServingLabelSnapshot,
                        i.EnergyKcal, i.ProteinG, i.CarbsG, i.FatG, i.FiberG))
                    .ToList()))
            .ToList();

        next.ReplaceStructure(copied);
        return next;
    }
}
