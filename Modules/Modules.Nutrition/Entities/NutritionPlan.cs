using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// Tenant-scoped nutrition plan template — a day's worth of prescribed meals + supplements. Immutable
/// version chain (rows share a <c>TemplateId</c>, each with an incrementing <c>Version</c>); edits clone a
/// new version rather than mutate in place. Direct port of the <c>WorkoutPlan</c> lifecycle.
/// </summary>
public sealed class NutritionPlan : AggregateRoot, ITenantEntity, ISoftDelete
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

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

    /// <summary>Deep-copies the current version into a new row (same TemplateId, Version + 1).</summary>
    public static NutritionPlan CreateNewVersion(
        NutritionPlan current,
        Guid createdBy,
        string name,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (createdBy == Guid.Empty) throw new DomainException("CreatedBy is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");

        var next = new NutritionPlan
        {
            Id = Guid.NewGuid(),
            TemplateId = current.TemplateId,
            TenantId = current.TenantId,
            CreatedBy = createdBy,
            Version = current.Version + 1,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
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
