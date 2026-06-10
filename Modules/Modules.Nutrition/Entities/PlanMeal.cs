using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>A meal slot in a nutrition plan (e.g. "Breakfast" at 07:30), not tied to a calendar date.</summary>
public sealed class PlanMeal : BaseEntity, ITenantEntity
{
    public Guid NutritionPlanId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = null!;
    public TimeOnly? ScheduledTime { get; private set; }
    public DayApplicability DayApplicability { get; private set; }

    private readonly List<PlanMealItem> _items = new();
    public IReadOnlyCollection<PlanMealItem> Items => _items;

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanMeal() { }

    public static PlanMeal Create(
        Guid nutritionPlanId,
        Guid tenantId,
        string name,
        int order,
        TimeOnly? scheduledTime,
        DayApplicability dayApplicability)
    {
        if (nutritionPlanId == Guid.Empty) throw new DomainException("NutritionPlanId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Meal name is required.");
        if (order < 1) throw new DomainException("order is out of range.");

        return new PlanMeal
        {
            Id = Guid.NewGuid(),
            NutritionPlanId = nutritionPlanId,
            TenantId = tenantId,
            Name = name.Trim(),
            Order = order,
            ScheduledTime = scheduledTime,
            DayApplicability = dayApplicability
        };
    }

    internal void AddItem(Guid tenantId, PlanMealItemData data)
        => _items.Add(PlanMealItem.Create(Id, tenantId, data));
}
