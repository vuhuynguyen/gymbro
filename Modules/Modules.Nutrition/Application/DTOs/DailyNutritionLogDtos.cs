using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.DTOs;

public sealed record LoggedItemDto(
    Guid Id,
    Guid? PlanMealItemId,
    bool IsPlanned,
    Guid? FoodId,
    string Kind,
    string FoodName,
    string ServingLabel,
    decimal Quantity,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG,
    LoggedItemStatus Status,
    DateTimeOffset? LoggedAtUtc,
    string? Note);

/// <summary>A day's items grouped into their meal slot for the checklist UI.</summary>
public sealed record LoggedMealDto(
    string Name,
    TimeOnly? ScheduledTime,
    IReadOnlyList<LoggedItemDto> Items);

public sealed record DailyNutritionLogDto(
    Guid? Id,
    Guid TraineeId,
    DateOnly LocalDate,
    DailyLogStatus Status,
    string Source,
    bool HasPlan,
    int AdherencePct,
    int PlannedCount,
    int CompletedCount,
    IReadOnlyList<LoggedMealDto> Meals);

public sealed record DailyNutritionLogSummaryDto(
    Guid Id,
    Guid TraineeId,
    DateOnly LocalDate,
    DailyLogStatus Status,
    string Source,
    int AdherencePct,
    int PlannedCount,
    int CompletedCount);

public sealed record DailyNutritionLogListDto(
    IReadOnlyList<DailyNutritionLogSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
