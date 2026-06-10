namespace Modules.NutritionModule.Application.DTOs;

/// <summary>
/// Point-in-time snapshot of a nutrition plan, stored as <c>jsonb</c> on the assignment at assign time and
/// read by the daily-log snapshot-on-touch step to seed a day's planned items. Analogous to the workout
/// session/assignment <c>SnapshotJson</c>. Enum stored as a string for forward/backward tolerance.
/// </summary>
public sealed record NutritionPlanSnapshot(
    Guid PlanId,
    int PlanVersion,
    string PlanName,
    IReadOnlyList<SnapshotMeal> Meals);

public sealed record SnapshotMeal(
    Guid PlanMealId,
    string Name,
    int Order,
    TimeOnly? ScheduledTime,
    string DayApplicability,
    IReadOnlyList<SnapshotItem> Items);

public sealed record SnapshotItem(
    Guid PlanMealItemId,
    Guid FoodId,
    int Order,
    decimal Quantity,
    string FoodName,
    string ServingLabel,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG);
