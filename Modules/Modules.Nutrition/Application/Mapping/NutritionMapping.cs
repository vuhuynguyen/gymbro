using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Mapping;

internal static class NutritionMapping
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    // ── Snapshot (jsonb on the assignment) ────────────────────────────────

    public static NutritionPlanSnapshot BuildSnapshot(NutritionPlan plan) =>
        new(
            plan.Id,
            plan.Version,
            plan.Name,
            plan.Meals
                .OrderBy(m => m.Order)
                .Select(m => new SnapshotMeal(
                    m.Id,
                    m.Name,
                    m.Order,
                    m.ScheduledTime,
                    m.DayApplicability.ToString(),
                    m.Items
                        .OrderBy(i => i.Order)
                        .Select(i => new SnapshotItem(
                            i.Id, i.FoodId, i.Order, i.Quantity, i.FoodNameSnapshot, i.ServingLabelSnapshot,
                            i.EnergyKcal, i.ProteinG, i.CarbsG, i.FatG, i.FiberG))
                        .ToList()))
                .ToList());

    public static string SerializeSnapshot(NutritionPlanSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    public static NutritionPlanSnapshot? DeserializeSnapshot(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<NutritionPlanSnapshot>(json, SnapshotJsonOptions);

    /// <summary>Flattens a plan snapshot into the seed data for a day's planned items. [kinds] maps each
    /// food id to its catalog kind name (resolved once at seed time) so planned supplements/beverages tag
    /// correctly; a food absent from the map defaults to "Food".</summary>
    public static IReadOnlyList<LoggedItemData> ToSeedItems(
        NutritionPlanSnapshot snapshot, IReadOnlyDictionary<Guid, string>? kinds = null) =>
        snapshot.Meals
            .OrderBy(m => m.Order)
            .SelectMany(m => m.Items
                .OrderBy(i => i.Order)
                .Select(i => new LoggedItemData(
                    i.PlanMealItemId,
                    m.Name,
                    m.ScheduledTime,
                    m.Order * 100 + i.Order, // stable ordering across meals
                    i.FoodId,
                    kinds != null && kinds.TryGetValue(i.FoodId, out var k) ? k : "Food",
                    i.FoodName,
                    i.ServingLabel,
                    i.Quantity,
                    i.EnergyKcal, i.ProteinG, i.CarbsG, i.FatG, i.FiberG)))
            .ToList();

    // ── Plan DTOs ─────────────────────────────────────────────────────────

    /// <summary>List projection — counts meals via a SQL subquery instead of loading the meal rows.
    /// LatestPublishedVersion is null here; the list handler patches it per template after projection.</summary>
    public static Expression<Func<NutritionPlan, NutritionPlanSummaryDto>> PlanSummaryProjection =>
        p => new NutritionPlanSummaryDto(
            p.Id, p.TemplateId, p.Version, p.Name, p.Description, p.CreatedOnUtc, p.Meals.Count, p.IsArchived,
            p.IsDraft, null);

    public static NutritionPlanDetailDto ToDetailDto(NutritionPlan plan, int? latestPublishedVersion = null) =>
        new(
            plan.Id,
            plan.TemplateId,
            plan.Version,
            plan.Name,
            plan.Description,
            plan.CreatedOnUtc,
            plan.Meals
                .OrderBy(m => m.Order)
                .Select(m => new PlanMealDto(
                    m.Id,
                    m.Order,
                    m.Name,
                    m.ScheduledTime,
                    m.DayApplicability,
                    m.Items
                        .OrderBy(i => i.Order)
                        .Select(i => new PlanMealItemDto(
                            i.Id, i.FoodId, i.Order, i.Quantity, i.FoodNameSnapshot, i.ServingLabelSnapshot,
                            i.EnergyKcal, i.ProteinG, i.CarbsG, i.FatG, i.FiberG))
                        .ToList()))
                .ToList(),
            plan.IsDraft,
            latestPublishedVersion);

    // ── Daily log DTOs ────────────────────────────────────────────────────

    public static LoggedItemDto ToItemDto(LoggedItem i) =>
        new(i.Id, i.PlanMealItemId, i.IsPlanned, i.FoodId, i.Kind, i.FoodNameSnapshot, i.ServingLabelSnapshot,
            i.Quantity, i.EnergyKcal, i.ProteinG, i.CarbsG, i.FatG, i.FiberG, i.Status, i.LoggedAtUtc, i.Note);

    public static DailyNutritionLogDto ToDayDto(DailyNutritionLog log)
    {
        var meals = log.Items
            .OrderBy(i => i.Order)
            .GroupBy(i => new { i.MealName, i.ScheduledTime })
            .OrderBy(g => g.Min(i => i.Order))
            .Select(g => new LoggedMealDto(
                g.Key.MealName,
                g.Key.ScheduledTime,
                g.OrderBy(i => i.Order).Select(ToItemDto).ToList()))
            .ToList();

        var planned = log.Items.Count(i => i.IsPlanned);
        var completed = log.Items.Count(i => i.IsAdherent);
        var adherence = log.Status == DailyLogStatus.Closed
            ? log.AdherencePct
            : DailyNutritionLog.ComputeAdherencePct(planned, completed);

        return new DailyNutritionLogDto(
            log.Id,
            log.TraineeId,
            log.LocalDate,
            log.Status,
            log.Source.ToString(),
            HasPlan: log.Source == NutritionSource.FromAssignment,
            adherence,
            planned,
            completed,
            meals);
    }

    /// <summary>An empty, non-persisted day for a trainee with no active nutrition assignment.</summary>
    public static DailyNutritionLogDto EmptyDay(Guid traineeId, DateOnly localDate) =>
        new(null, traineeId, localDate, DailyLogStatus.Open, NutritionSource.Adhoc.ToString(),
            HasPlan: false, AdherencePct: 100, PlannedCount: 0, CompletedCount: 0, Meals: []);

    /// <summary>
    /// List projection — computes the planned/adherent counts in SQL via correlated subqueries, so a day
    /// summary loads neither the item rows nor the jsonb snapshot. Shared by the trainee history and coach
    /// adherence lists. Adherence itself is finished in memory (<see cref="ToSummaryDto"/>) because the
    /// rounding rule isn't SQL-translatable.
    /// </summary>
    public static Expression<Func<DailyNutritionLog, DailyLogCounts>> SummaryRowProjection =>
        l => new DailyLogCounts(
            l.Id, l.TraineeId, l.LocalDate, l.Status, l.Source, l.AdherencePct,
            l.Items.Count(i => i.PlanMealItemId != null),
            l.Items.Count(i => i.PlanMealItemId != null
                && (i.Status == LoggedItemStatus.Completed || i.Status == LoggedItemStatus.Substituted)));

    // ── Metric entries (daily check-in) ───────────────────────────────────

    public static MetricEntryDto ToMetricDto(MetricEntry e) =>
        new(e.Type, e.Value, e.Unit, e.LocalDate, e.LoggedAtUtc);

    public static DailyNutritionLogSummaryDto ToSummaryDto(DailyLogCounts r) =>
        new(r.Id, r.TraineeId, r.LocalDate, r.Status, r.Source.ToString(),
            r.Status == DailyLogStatus.Closed
                ? r.StoredAdherence
                : DailyNutritionLog.ComputeAdherencePct(r.PlannedCount, r.CompletedCount),
            r.PlannedCount, r.CompletedCount);
}

/// <summary>Projected day counts (computed in SQL) used to build a list summary without loading items.</summary>
internal sealed record DailyLogCounts(
    Guid Id,
    Guid TraineeId,
    DateOnly LocalDate,
    DailyLogStatus Status,
    NutritionSource Source,
    int StoredAdherence,
    int PlannedCount,
    int CompletedCount);
