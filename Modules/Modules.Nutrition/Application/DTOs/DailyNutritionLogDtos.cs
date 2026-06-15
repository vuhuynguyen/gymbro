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

// ── Progress page — nutrition-adherence trend (api/me/progress/nutrition-adherence, Phase 3) ──

/// <summary>
/// The caller's nutrition-plan adherence over a short window (default 4 weeks). <see cref="HasPlan"/> is
/// false (with empty <see cref="Days"/> and a null <see cref="CurrentWeekAvgPct"/>) when the user has never
/// had a planned nutrition day. <see cref="Days"/> lists only days that have a logged plan day in range, one
/// per local date. <see cref="CurrentWeekAvgPct"/> is the mean <c>AdherencePct</c> over the current local
/// week's planned days (null when none).
/// <para>
/// The adherence trend (<see cref="Days"/> / <see cref="CurrentWeekAvgPct"/>) stays <b>plan-only</b> on
/// purpose (Decision <b>D15</b>): an ad-hoc self-logged day is 100% by convention, so folding it in would
/// fake a perfect record. Ad-hoc logging is surfaced instead as a separate <i>tracking</i> signal:
/// <see cref="LoggedDaysThisWeek"/> counts the current local week's days the caller actually logged food on
/// (any source), and <see cref="HasAnyLogging"/> is whether they have ever logged a nutrition day. These let
/// a plan-less, self-logging user be recognized ("you logged 5 days this week") without inflating an
/// adherence % they have no plan to adhere to.
/// </para>
/// Query-only — rides the existing <c>DailyNutritionLog.AdherencePct</c>, no new entity, no migration.
/// </summary>
public sealed record NutritionAdherenceDto(
    bool HasPlan,
    IReadOnlyList<DailyAdherenceDto> Days,
    int? CurrentWeekAvgPct,
    int LoggedDaysThisWeek,
    bool HasAnyLogging);

/// <summary>One day's nutrition-plan adherence (planned-item count, completed/substituted count, %).</summary>
public sealed record DailyAdherenceDto(
    DateOnly LocalDate,
    int AdherencePct,
    int PlannedCount,
    int CompletedCount);
