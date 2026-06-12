using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.DTOs;

public sealed record NutritionPlanSummaryDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string Name,
    string? Description,
    DateTimeOffset CreatedOnUtc,
    int MealCount,
    bool IsArchived);

public sealed record NutritionPlanListDto(
    IReadOnlyList<NutritionPlanSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlanMealItemDto(
    Guid Id,
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

public sealed record PlanMealDto(
    Guid Id,
    int Order,
    string Name,
    TimeOnly? ScheduledTime,
    DayApplicability DayApplicability,
    IReadOnlyList<PlanMealItemDto> Items);

public sealed record NutritionPlanDetailDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string Name,
    string? Description,
    DateTimeOffset CreatedOnUtc,
    IReadOnlyList<PlanMealDto> Meals);
