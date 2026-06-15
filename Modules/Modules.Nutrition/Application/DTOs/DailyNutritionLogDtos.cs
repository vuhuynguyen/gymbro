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
    IReadOnlyList<LoggedMealDto> Meals,
    int ConsumedKcal,
    int? TargetKcal);

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
/// <para>
/// <see cref="CaloriesByDay"/> is the ALL-SOURCE per-day calorie list (Decision <b>D15</b>'s companion to the
/// plan-only <see cref="Days"/>): every day in the endpoint window that carries at least one logged item — ANY
/// source, plan or ad-hoc — date-ascending, so an ad-hoc / no-plan self-logger (who gets an empty <see cref="Days"/>)
/// still surfaces what they actually logged. Each entry's <c>ConsumedKcal</c> / <c>TargetKcal</c> carry the same
/// semantics as the per-day totals on <see cref="DailyAdherenceDto"/> (consumed = adherent kcal, all sources;
/// target = planned-meal kcal, plan-only, null when no plan / no planned energy / <c>HideMacroTargets</c>). A
/// touched-but-empty day (no logged item) is omitted.
/// </para>
/// Query-only — rides the existing <c>DailyNutritionLog.AdherencePct</c>, no new entity, no migration.
/// </summary>
public sealed record NutritionAdherenceDto(
    bool HasPlan,
    IReadOnlyList<DailyAdherenceDto> Days,
    int? CurrentWeekAvgPct,
    int LoggedDaysThisWeek,
    bool HasAnyLogging,
    IReadOnlyList<DayCaloriesDto> CaloriesByDay);

/// <summary>
/// One day's nutrition-plan adherence (planned-item count, completed/substituted count, %).
/// <para>
/// <see cref="ConsumedKcal"/> is the rounded sum of <c>EnergyKcal × Quantity</c> over the day's adherent
/// (Completed/Substituted) items across <b>all sources</b> — an ad-hoc, plan-less day still reports the
/// calories it logged. <see cref="TargetKcal"/> is the rounded sum over the day's <b>planned</b> items
/// (the prescribed energy goal); it is <c>null</c> — never fabricated — when the day has no planned items,
/// the planned items carry no energy macros, or the governing assignment hides macro targets
/// (<c>HideMacroTargets</c>). The calorie totals are a parallel signal to <see cref="AdherencePct"/>, which
/// stays plan-only and byte-for-byte unchanged.
/// </para>
/// </summary>
public sealed record DailyAdherenceDto(
    DateOnly LocalDate,
    int AdherencePct,
    int PlannedCount,
    int CompletedCount,
    int ConsumedKcal,
    int? TargetKcal);

/// <summary>
/// One day's ALL-SOURCE calorie totals for <see cref="NutritionAdherenceDto.CaloriesByDay"/>: <see cref="ConsumedKcal"/>
/// is the rounded sum of <c>EnergyKcal × Quantity</c> over the day's adherent (Completed/Substituted) items across
/// <b>all sources</b> (plan or ad-hoc); <see cref="TargetKcal"/> is the rounded sum over the day's <b>planned</b>
/// items (the prescribed energy goal), <c>null</c> — never fabricated — when the day has no planned items, the planned
/// items carry no energy, or the governing assignment hides macro targets (<c>HideMacroTargets</c>). Same semantics
/// as the per-day totals on <see cref="DailyAdherenceDto"/>; only days that carry a logged item appear in the list.
/// </summary>
public sealed record DayCaloriesDto(
    DateOnly LocalDate,
    int ConsumedKcal,
    int? TargetKcal);
