using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>A prescribed food in a meal. Carries a target-nutrition snapshot captured at authoring time.</summary>
public sealed class PlanMealItem : BaseEntity, ITenantEntity
{
    public Guid PlanMealId { get; private set; }
    public Guid FoodId { get; private set; }
    public int Order { get; private set; }
    public decimal Quantity { get; private set; }

    public string FoodNameSnapshot { get; private set; } = null!;
    public string ServingLabelSnapshot { get; private set; } = null!;
    public decimal? EnergyKcal { get; private set; }
    public decimal? ProteinG { get; private set; }
    public decimal? CarbsG { get; private set; }
    public decimal? FatG { get; private set; }
    public decimal? FiberG { get; private set; }

    Guid ITenantEntity.TenantId => TenantId!.Value;

    private PlanMealItem() { }

    public static PlanMealItem Create(Guid planMealId, Guid tenantId, PlanMealItemData data)
    {
        if (planMealId == Guid.Empty) throw new DomainException("PlanMealId is required.");
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        if (data.FoodId == Guid.Empty) throw new DomainException("FoodId is required.");
        if (data.Quantity <= 0) throw new DomainException("Quantity must be positive.");

        return new PlanMealItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanMealId = planMealId,
            FoodId = data.FoodId,
            Order = data.Order,
            Quantity = data.Quantity,
            FoodNameSnapshot = string.IsNullOrWhiteSpace(data.FoodNameSnapshot) ? "(food)" : data.FoodNameSnapshot.Trim(),
            ServingLabelSnapshot = string.IsNullOrWhiteSpace(data.ServingLabelSnapshot) ? "1 serving" : data.ServingLabelSnapshot.Trim(),
            EnergyKcal = data.EnergyKcal,
            ProteinG = data.ProteinG,
            CarbsG = data.CarbsG,
            FatG = data.FatG,
            FiberG = data.FiberG
        };
    }
}
