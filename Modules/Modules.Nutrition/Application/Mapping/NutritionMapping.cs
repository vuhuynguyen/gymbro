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
        NutritionPlanSnapshot snapshot, bool isTrainingDay, IReadOnlyDictionary<Guid, string>? kinds = null) =>
        snapshot.Meals
            // Recurrence: seed only the meals that apply to this day's training/rest type (EveryDay always).
            .Where(m => NutritionScheduleRules.Applies(ParseDayApplicability(m.DayApplicability), isTrainingDay))
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

    /// <summary>The snapshot stores DayApplicability as a string (forward-tolerant); parse it back for the rule,
    /// defaulting to EveryDay on any unknown value so a planned meal is never silently dropped.</summary>
    private static DayApplicability ParseDayApplicability(string value) =>
        Enum.TryParse<DayApplicability>(value, ignoreCase: true, out var applicability)
            ? applicability
            : DayApplicability.EveryDay;

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

    // ── Per-day calorie totals (Product: target = planned-meal kcal, plan-only; consumed = adherent kcal, all
    //    sources). Same rounding rule (AwayFromZero) and HONESTY gate (target null when no planned energy) the
    //    SQL projection mirrors below, so the day read and the adherence trend agree byte-for-byte. ──

    /// <summary>An item is adherent for the consumed-kcal sum when Completed or Substituted — ANY source, so an
    /// ad-hoc add (created already Completed) and a ticked/substituted planned item both count, exactly like
    /// <see cref="HasLoggedItem"/>.</summary>
    private static bool CountsAsConsumed(LoggedItem i) =>
        i.Status is LoggedItemStatus.Completed or LoggedItemStatus.Substituted;

    /// <summary>round(Σ EnergyKcal×Quantity) over the adherent items (all sources). Always an int (0 when none).</summary>
    private static int ConsumedKcalOf(IEnumerable<LoggedItem> items) =>
        RoundKcal(items.Where(CountsAsConsumed).Sum(i => (i.EnergyKcal ?? 0m) * i.Quantity));

    /// <summary>round(Σ EnergyKcal×Quantity) over the PLANNED items — the prescribed energy target. Null (never
    /// fabricated) when the day has no planned item carrying an EnergyKcal value; macro-hiding is applied by the
    /// caller (the read's redaction / the trend's per-day HideMacroTargets gate).</summary>
    private static int? TargetKcalOf(IEnumerable<LoggedItem> items)
    {
        var planned = items.Where(i => i.IsPlanned).ToList();
        return planned.Any(i => i.EnergyKcal != null)
            ? RoundKcal(planned.Sum(i => (i.EnergyKcal ?? 0m) * i.Quantity))
            : null;
    }

    private static int RoundKcal(decimal kcal) => (int)Math.Round(kcal, MidpointRounding.AwayFromZero);

    public static DailyNutritionLogDto ToDayDto(DailyNutritionLog log)
    {
        // Group by meal NAME only so ad-hoc items (which carry no ScheduledTime) merge into the matching
        // planned meal instead of forming a second same-named section. The section keeps the plan's
        // scheduled time (the first non-null), and the section orders by its earliest item — so a planned
        // meal stays in place and its ad-hoc additions sort to the end of that meal.
        var meals = log.Items
            .OrderBy(i => i.Order)
            .GroupBy(i => i.MealName)
            .OrderBy(g => g.Min(i => i.Order))
            .Select(g => new LoggedMealDto(
                g.Key,
                g.Select(i => i.ScheduledTime).FirstOrDefault(t => t.HasValue),
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
            meals,
            ConsumedKcal: ConsumedKcalOf(log.Items),
            TargetKcal: TargetKcalOf(log.Items));
    }

    /// <summary>An empty, non-persisted day for a trainee with no active nutrition assignment.</summary>
    public static DailyNutritionLogDto EmptyDay(Guid traineeId, DateOnly localDate) =>
        new(null, traineeId, localDate, DailyLogStatus.Open, NutritionSource.Adhoc.ToString(),
            HasPlan: false, AdherencePct: 100, PlannedCount: 0, CompletedCount: 0, Meals: [],
            ConsumedKcal: 0, TargetKcal: null);

    /// <summary>
    /// Trainee-facing redaction for the assignment's <c>HideMacroTargets</c> (filter-on-read, the nutrition
    /// sibling of workout <c>RedactSnapshotTargets</c>): nulls the macro numbers on PLANNED items so the trainee
    /// sees WHAT to eat (meal, food, serving, quantity) but not the coach's macro TARGETS. The stored items are
    /// untouched — coach/admin reads and day-close adherence keep the real macros — and ad-hoc items the trainee
    /// logged themselves keep their macros (their own data, not a target).
    /// </summary>
    public static DailyNutritionLogDto RedactPlannedMacros(DailyNutritionLogDto day) =>
        day with
        {
            // The per-day TargetKcal is a planned-macro target — redacted to null with the rest (never fabricated).
            // ConsumedKcal is the trainee's own logged energy (not a coach target), so it stays.
            TargetKcal = null,
            Meals = day.Meals
                .Select(m => m with
                {
                    Items = m.Items
                        .Select(i => i.IsPlanned
                            ? i with { EnergyKcal = null, ProteinG = null, CarbsG = null, FatG = null, FiberG = null }
                            : i)
                        .ToList()
                })
                .ToList()
        };

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

    /// <summary>
    /// SQL predicate for "this day has at least one actually-logged food item" — any source. An ad-hoc item is
    /// created already <c>Completed</c> and a planned item the trainee ticked is <c>Completed</c>/<c>Substituted</c>,
    /// so both count; <c>Planned</c>/<c>Skipped</c>/<c>Missed</c> placeholders do not. Used by the nutrition-adherence
    /// read's ad-hoc <i>tracking</i> signal (D15) — unlike <see cref="SummaryRowProjection"/>'s CompletedCount it is
    /// NOT restricted to planned items, so a pure self-logged day registers as "logged".
    /// </summary>
    public static Expression<Func<DailyNutritionLog, bool>> HasLoggedItem =>
        l => l.Items.Any(i =>
            i.Status == LoggedItemStatus.Completed || i.Status == LoggedItemStatus.Substituted);

    // ── Metric entries (daily check-in) ───────────────────────────────────

    public static MetricEntryDto ToMetricDto(MetricEntry e) =>
        new(e.Type, e.Value, e.Unit, e.LocalDate, e.LoggedAtUtc);

    public static DailyNutritionLogSummaryDto ToSummaryDto(DailyLogCounts r) =>
        new(r.Id, r.TraineeId, r.LocalDate, r.Status, r.Source.ToString(),
            r.Status == DailyLogStatus.Closed
                ? r.StoredAdherence
                : DailyNutritionLog.ComputeAdherencePct(r.PlannedCount, r.CompletedCount),
            r.PlannedCount, r.CompletedCount);

    /// <summary>
    /// Adherence-trend projection — the same SQL count subqueries as <see cref="SummaryRowProjection"/> PLUS the
    /// per-day kcal sums and the honesty flag, computed in SQL without loading item rows. Used ONLY by the Progress
    /// nutrition-adherence read (<see cref="ToAdherenceDto"/>); the list/history readers use the lighter
    /// <see cref="SummaryRowProjection"/> so they never pay for the kcal aggregates they discard.
    /// </summary>
    public static Expression<Func<DailyNutritionLog, DailyAdherenceCounts>> AdherenceRowProjection =>
        l => new DailyAdherenceCounts(
            l.LocalDate, l.Status, l.AdherencePct,
            l.Items.Count(i => i.PlanMealItemId != null),
            l.Items.Count(i => i.PlanMealItemId != null
                && (i.Status == LoggedItemStatus.Completed || i.Status == LoggedItemStatus.Substituted)),
            l.NutritionPlanAssignmentId,
            // ConsumedKcal — Σ EnergyKcal×Quantity over the adherent (Completed/Substituted) items, ALL SOURCES.
            // Summed in SQL as decimal (null kcal ⇒ 0); finalized (rounded to int) in ToAdherenceDto.
            l.Items
                .Where(i => i.Status == LoggedItemStatus.Completed || i.Status == LoggedItemStatus.Substituted)
                .Sum(i => (i.EnergyKcal ?? 0m) * i.Quantity),
            // TargetKcal — Σ over PLANNED items, plus a flag for whether ANY planned item carries energy. The
            // flag is the honesty gate (target stays null when no planned energy exists, never a fabricated 0).
            l.Items
                .Where(i => i.PlanMealItemId != null)
                .Sum(i => (i.EnergyKcal ?? 0m) * i.Quantity),
            l.Items.Any(i => i.PlanMealItemId != null && i.EnergyKcal != null));

    /// <summary>
    /// Builds the Progress per-day adherence point (the same byte-for-byte AdherencePct as <see cref="ToSummaryDto"/>)
    /// and adds the calorie totals from the SQL-projected sums. <paramref name="hideMacroTargets"/> is the governing
    /// assignment's macro-hiding flag: when true the planned target is redacted to null (never fabricated), exactly
    /// like the day read's <see cref="RedactPlannedMacros"/>. The honesty gate also nulls the target when the day
    /// carries no planned energy at all.
    /// </summary>
    public static DailyAdherenceDto ToAdherenceDto(DailyAdherenceCounts r, bool hideMacroTargets) =>
        new(
            r.LocalDate,
            r.Status == DailyLogStatus.Closed
                ? r.StoredAdherence
                : DailyNutritionLog.ComputeAdherencePct(r.PlannedCount, r.CompletedCount),
            r.PlannedCount,
            r.CompletedCount,
            ConsumedKcal: RoundKcal(r.ConsumedKcalSum),
            TargetKcal: hideMacroTargets || !r.HasPlannedEnergy ? null : RoundKcal(r.PlannedKcalSum));

    /// <summary>
    /// Builds an ALL-SOURCE per-day calorie point (D15) from the same SQL-projected sums as <see cref="ToAdherenceDto"/>.
    /// <c>ConsumedKcal</c> is the adherent-item sum across all sources (so an ad-hoc / no-plan day still reports its
    /// logged energy); <c>TargetKcal</c> reuses the identical honesty gate — null when the governing assignment hides
    /// macro targets (<paramref name="hideMacroTargets"/>) or the day carries no planned energy at all.
    /// </summary>
    public static DayCaloriesDto ToDayCaloriesDto(DailyAdherenceCounts r, bool hideMacroTargets) =>
        new(
            r.LocalDate,
            ConsumedKcal: RoundKcal(r.ConsumedKcalSum),
            TargetKcal: hideMacroTargets || !r.HasPlannedEnergy ? null : RoundKcal(r.PlannedKcalSum));
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

/// <summary>Projected day counts for the Progress adherence trend (computed in SQL) without loading items. The kcal
/// sums are decimal SQL aggregates (rounded to int in the mapping); <see cref="HasPlannedEnergy"/> is the honesty
/// gate that keeps a no-planned-energy day's target null rather than a fabricated 0.</summary>
internal sealed record DailyAdherenceCounts(
    DateOnly LocalDate,
    DailyLogStatus Status,
    int StoredAdherence,
    int PlannedCount,
    int CompletedCount,
    Guid? NutritionPlanAssignmentId,
    decimal ConsumedKcalSum,
    decimal PlannedKcalSum,
    bool HasPlannedEnergy);
