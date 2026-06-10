namespace Modules.NutritionModule.Entities;

/// <summary>
/// Carrier for a planned food item during a plan-structure replace. The macro fields are a snapshot of the
/// referenced food captured at authoring time (resolved by the handler via the Food contract) so a later
/// catalog edit never silently moves a coach's targets.
/// </summary>
public sealed record PlanMealItemData(
    Guid FoodId,
    int Order,
    decimal Quantity,
    string FoodNameSnapshot,
    string ServingLabelSnapshot,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG);

/// <summary>Carrier for a planned meal (slot) during a plan-structure replace.</summary>
public sealed record PlanMealData(
    string Name,
    int Order,
    TimeOnly? ScheduledTime,
    DayApplicability DayApplicability,
    IReadOnlyList<PlanMealItemData> Items);
