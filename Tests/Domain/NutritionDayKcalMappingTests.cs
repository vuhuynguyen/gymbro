using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Domain;

/// <summary>
/// Per-day calorie totals on the trainee day DTO (Log tab's "today" + "day-by-date" reads, via
/// <see cref="NutritionMapping.ToDayDto"/> and the <see cref="NutritionMapping.RedactPlannedMacros"/> hiding
/// path). Product rule: <c>ConsumedKcal</c> = round(Σ EnergyKcal×Quantity) over adherent (Completed/Substituted)
/// items across ALL sources; <c>TargetKcal</c> = round(Σ over PLANNED items), null (never fabricated) when the
/// day has no planned items, the planned items carry no energy, or macro targets are hidden. AdherencePct stays
/// plan-only and byte-for-byte unchanged.
/// </summary>
public sealed class NutritionDayKcalMappingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 15);

    private static LoggedItemData PlannedItem(int order, decimal? kcal, decimal qty = 1m)
        => new(
            PlanMealItemId: Guid.NewGuid(),
            MealName: "Breakfast",
            ScheduledTime: null,
            Order: order,
            FoodId: Guid.NewGuid(),
            Kind: "Food",
            FoodNameSnapshot: "Oats",
            ServingLabelSnapshot: "1 bowl",
            Quantity: qty,
            EnergyKcal: kcal, ProteinG: 10m, CarbsG: 50m, FatG: 6m, FiberG: 8m);

    private static LoggedItemData AdhocItem(int order, decimal? kcal, decimal qty = 1m)
        => new(
            PlanMealItemId: null,
            MealName: "Snack",
            ScheduledTime: null,
            Order: order,
            FoodId: Guid.NewGuid(),
            Kind: "Food",
            FoodNameSnapshot: "Banana",
            ServingLabelSnapshot: "1 medium",
            Quantity: qty,
            EnergyKcal: kcal, ProteinG: 1m, CarbsG: 23m, FatG: 0m, FiberG: 3m);

    private static DailyNutritionLog PlannedDay(IEnumerable<LoggedItemData> planned)
    {
        var log = DailyNutritionLog.Open(
            Guid.NewGuid(), Tenant, Day, "UTC", NutritionSource.FromAssignment, Guid.NewGuid(), null);
        log.SeedPlannedItems(planned);
        return log;
    }

    private static DailyNutritionLog AdhocDay()
        => DailyNutritionLog.OpenSelfLogged(Guid.NewGuid(), Tenant, Day, "UTC");

    // ── ConsumedKcal — all sources ──

    [Fact]
    public void ConsumedKcal_counts_adhoc_items_on_a_plan_less_day()
    {
        var log = AdhocDay();
        log.AddAdhocItem(AdhocItem(0, kcal: 90m), note: null);          // Completed on create
        log.AddAdhocItem(AdhocItem(1, kcal: 250m, qty: 2m), note: null); // 250×2 = 500

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Equal(590, dto.ConsumedKcal);
        Assert.Null(dto.TargetKcal);     // no planned items ⇒ no target (never fabricated)
        Assert.Equal(100, dto.AdherencePct); // pure ad-hoc ⇒ 100% by convention, unchanged
    }

    [Fact]
    public void ConsumedKcal_includes_completed_planned_items_and_excludes_un_ticked_ones()
    {
        var log = PlannedDay(new[] { PlannedItem(0, 300m), PlannedItem(1, 300m) });
        log.Items.First().Complete(null); // one planned item ticked; the other stays Planned

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Equal(300, dto.ConsumedKcal); // only the completed planned item
        Assert.Equal(600, dto.TargetKcal);   // both planned items count toward the target
        Assert.Equal(50, dto.AdherencePct);  // 1/2 completed
    }

    // ── TargetKcal — honesty gates ──

    [Fact]
    public void TargetKcal_is_null_on_a_no_plan_day()
    {
        var log = AdhocDay();
        log.AddAdhocItem(AdhocItem(0, kcal: 120m), note: null);

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Null(dto.TargetKcal);
        Assert.Equal(120, dto.ConsumedKcal);
    }

    [Fact]
    public void TargetKcal_is_null_when_planned_items_carry_no_energy()
    {
        // Planned items exist but none has an EnergyKcal value — target is unknown, never a fabricated 0.
        var log = PlannedDay(new[] { PlannedItem(0, kcal: null), PlannedItem(1, kcal: null) });

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Null(dto.TargetKcal);
    }

    [Fact]
    public void TargetKcal_is_null_when_macro_targets_are_hidden()
    {
        var log = PlannedDay(new[] { PlannedItem(0, 300m), PlannedItem(1, 300m) });
        log.Items.First().Complete(null);
        var dto = NutritionMapping.ToDayDto(log);
        Assert.Equal(600, dto.TargetKcal); // visible before redaction

        var redacted = NutritionMapping.RedactPlannedMacros(dto);

        Assert.Null(redacted.TargetKcal);            // target redacted with the planned macros
        Assert.Equal(dto.ConsumedKcal, redacted.ConsumedKcal); // the trainee's own logged energy is kept
        Assert.Equal(300, redacted.ConsumedKcal);
        Assert.Equal(dto.AdherencePct, redacted.AdherencePct); // adherence unchanged by hiding
    }

    // ── mixed plan + ad-hoc day ──

    [Fact]
    public void Mixed_plan_and_adhoc_day_reports_both_totals_correctly()
    {
        // 3 planned @ 300 (target 900), 2 completed (600 consumed from plan); 1 substituted-equivalent handled via
        // Complete; plus 2 ad-hoc items (90 + 150) consumed. Target stays plan-only.
        var log = PlannedDay(new[] { PlannedItem(0, 300m), PlannedItem(1, 300m), PlannedItem(2, 300m) });
        var planned = log.Items.ToList();
        planned[0].Complete(null);
        planned[1].Complete(null);
        log.AddAdhocItem(AdhocItem(10, kcal: 90m), note: null);
        log.AddAdhocItem(AdhocItem(11, kcal: 150m), note: null);

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Equal(900, dto.TargetKcal);                  // 3 planned × 300, plan-only
        Assert.Equal(600 + 90 + 150, dto.ConsumedKcal);     // 2 completed planned + 2 ad-hoc = 840
        Assert.Equal(67, dto.AdherencePct);                 // 2/3 ⇒ 66.67 → 67 (unchanged)
    }

    // ── AdherencePct is byte-for-byte unchanged by the new fields ──

    [Fact]
    public void AdherencePct_and_counts_are_unchanged_by_the_kcal_fields()
    {
        var log = PlannedDay(new[] { PlannedItem(0, 300m), PlannedItem(1, 300m), PlannedItem(2, 300m), PlannedItem(3, 300m) });
        var items = log.Items.ToList();
        items[0].Complete(null);
        items[1].Substitute(Guid.NewGuid(), "Food", "Swap", "1", 320m, 320m, 12m, 40m, 5m, 4m, null);

        var dto = NutritionMapping.ToDayDto(log);

        Assert.Equal(4, dto.PlannedCount);
        Assert.Equal(2, dto.CompletedCount);          // completed + substituted are both adherent
        Assert.Equal(50, dto.AdherencePct);           // 2/4
        Assert.True(dto.HasPlan);
    }
}
