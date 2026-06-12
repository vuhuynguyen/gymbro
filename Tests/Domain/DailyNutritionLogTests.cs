using Modules.NutritionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class DailyNutritionLogTests
{
    private static DailyNutritionLog OpenLog(Guid? traineeId = null, Guid? tenantId = null) =>
        DailyNutritionLog.Open(
            traineeId ?? Guid.NewGuid(),
            tenantId ?? Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            clientTimezone: "Europe/London",
            source: NutritionSource.FromAssignment,
            assignmentId: Guid.NewGuid(),
            snapshotJson: "{}");

    private static LoggedItemData Planned(int order, string name = "Oats") =>
        new(Guid.NewGuid(), $"Meal {order}", new TimeOnly(8, 0), order, Guid.NewGuid(), "Food", name, "1 bowl", 1m,
            300m, 10m, 50m, 6m, 8m);

    private static LoggedItemData Adhoc() =>
        new(null, "Off-plan", null, 0, Guid.NewGuid(), "Food", "Pizza slice", "1 slice", 2m, 285m, 12m, 36m, 10m, 2m);

    // ── Open + seed (snapshot-on-touch) ───────────────────────────────────

    [Fact]
    public void Open_starts_an_open_day()
    {
        var log = OpenLog();

        Assert.True(log.IsOpen);
        Assert.Equal(0, log.AdherencePct);
        Assert.Empty(log.Items);
    }

    [Fact]
    public void OpenSelfLogged_is_an_open_plan_less_adhoc_day()
    {
        var tenantId = Guid.NewGuid();
        var log = DailyNutritionLog.OpenSelfLogged(Guid.NewGuid(), tenantId, new DateOnly(2026, 6, 10), "UTC");

        Assert.True(log.IsOpen);
        Assert.Equal(NutritionSource.Adhoc, log.Source);
        Assert.Null(log.NutritionPlanAssignmentId);
        Assert.Null(log.SnapshotJson);
        Assert.Equal(tenantId, log.TenantId);
        Assert.Empty(log.Items);
    }

    [Fact]
    public void OpenSelfLogged_with_an_adhoc_item_closes_at_full_adherence()
    {
        var log = DailyNutritionLog.OpenSelfLogged(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 6, 10), "UTC");
        log.AddAdhocItem(Adhoc(), note: null);

        log.Close();

        Assert.Equal(DailyLogStatus.Closed, log.Status);
        Assert.Equal(100, log.AdherencePct); // no planned items → 100% by convention
    }

    [Fact]
    public void SeedPlannedItems_seeds_items_as_Planned_with_durable_snapshot()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2) });

        Assert.Equal(2, log.Items.Count);
        Assert.All(log.Items, i => Assert.Equal(LoggedItemStatus.Planned, i.Status));
        Assert.All(log.Items, i => Assert.NotNull(i.PlanMealItemId)); // planned, not ad-hoc
        Assert.Contains(log.Items, i => i.FoodNameSnapshot == "Oats"); // snapshot captured
    }

    // ── Completion-first status transitions ───────────────────────────────

    [Fact]
    public void Complete_marks_item_completed_and_stamps_time()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var item = log.Items.First();

        item.Complete("ate it");

        Assert.Equal(LoggedItemStatus.Completed, item.Status);
        Assert.NotNull(item.LoggedAtUtc);
        Assert.Equal("ate it", item.Note);
    }

    [Fact]
    public void Skip_marks_item_skipped()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var item = log.Items.First();

        item.Skip(null);

        Assert.Equal(LoggedItemStatus.Skipped, item.Status);
    }

    [Fact]
    public void Substitute_swaps_food_and_keeps_provenance()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var item = log.Items.First();
        var originalFood = item.FoodId;
        var newFood = Guid.NewGuid();

        item.Substitute(newFood, "Food", "Greek yoghurt", "1 cup", 1m, 130m, 11m, 9m, 4m, 0m, null);

        Assert.Equal(LoggedItemStatus.Substituted, item.Status);
        Assert.Equal(newFood, item.FoodId);
        Assert.Equal(originalFood, item.SubstitutedFromFoodId); // provenance preserved
        Assert.Equal("Greek yoghurt", item.FoodNameSnapshot);
    }

    [Fact]
    public void AddAdhocItem_appends_a_completed_off_plan_item()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });

        var adhoc = log.AddAdhocItem(Adhoc(), "couldn't resist");

        Assert.Equal(2, log.Items.Count);
        Assert.Null(adhoc.PlanMealItemId); // ad-hoc
        Assert.Equal(LoggedItemStatus.Completed, adhoc.Status);
    }

    // ── Re-transition contract ─────────────────────────────────────────────
    // The entity deliberately allows undo-by-overwrite between the directly settable statuses
    // (Complete ↔ Skip — the handler gates *which* statuses may be set and whether the day is open;
    // the entity does not freeze them), while MarkMissedIfPlanned ignores anything that is no longer
    // Planned, and removing a planned item is rejected outright.

    [Fact]
    public void Skip_after_Complete_overwrites_status_undo_by_design()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var item = log.Items.First();

        item.Complete("ate it");
        item.Skip("actually didn't");

        Assert.Equal(LoggedItemStatus.Skipped, item.Status);
        Assert.Equal("actually didn't", item.Note);
        Assert.NotNull(item.LoggedAtUtc);
    }

    [Fact]
    public void Complete_after_Skip_overwrites_status_undo_by_design()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var item = log.Items.First();

        item.Skip(null);
        item.Complete(null);

        Assert.Equal(LoggedItemStatus.Completed, item.Status);
    }

    [Fact]
    public void MarkMissedIfPlanned_ignores_non_planned_statuses()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2), Planned(3), Planned(4) });
        var items = log.Items.ToList();
        items[0].Complete(null);
        items[1].Skip(null);
        items[2].Substitute(Guid.NewGuid(), "Food", "alt", "1", 1m, null, null, null, null, null, null);
        // items[3] left Planned

        foreach (var item in items)
            item.MarkMissedIfPlanned();

        Assert.Equal(LoggedItemStatus.Completed, items[0].Status);   // untouched
        Assert.Equal(LoggedItemStatus.Skipped, items[1].Status);     // untouched
        Assert.Equal(LoggedItemStatus.Substituted, items[2].Status); // untouched
        Assert.Equal(LoggedItemStatus.Missed, items[3].Status);      // only Planned transitions
    }

    [Fact]
    public void RemoveAdhocItem_rejects_planned_items()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        var planned = log.Items.First();

        Assert.Throws<BuildingBlocks.Shared.DomainPrimitives.DomainException>(
            () => log.RemoveAdhocItem(planned));
        Assert.Single(log.Items); // still there — planned items are skipped, never deleted
    }

    // ── Adherence ─────────────────────────────────────────────────────────

    [Fact]
    public void Adherence_counts_completed_and_substituted_planned_items()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2), Planned(3), Planned(4) });
        var items = log.Items.ToList();
        items[0].Complete(null);
        items[1].Substitute(Guid.NewGuid(), "Food", "alt", "1", 1m, null, null, null, null, null, null);
        items[2].Skip(null);
        // items[3] left Planned

        Assert.Equal(50, DailyNutritionLog.ComputeAdherencePct(log.Items)); // 2 of 4 adherent
    }

    [Fact]
    public void Adherence_ignores_adhoc_items_in_the_denominator()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2) });
        log.Items.First().Complete(null);
        log.AddAdhocItem(Adhoc(), null); // ad-hoc completed — must not change the planned ratio

        Assert.Equal(50, DailyNutritionLog.ComputeAdherencePct(log.Items)); // 1 of 2 planned
    }

    [Fact]
    public void Pure_adhoc_day_is_full_adherence()
    {
        var log = OpenLog();
        log.AddAdhocItem(Adhoc(), null);

        Assert.Equal(100, DailyNutritionLog.ComputeAdherencePct(log.Items)); // no planned items
    }

    // ── Close (Missed + finalize + event) ─────────────────────────────────

    [Fact]
    public void Close_marks_still_planned_items_Missed_distinct_from_Skipped()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2), Planned(3) });
        var items = log.Items.ToList();
        items[0].Complete(null);
        items[1].Skip(null);
        // items[2] left Planned → should become Missed

        log.Close();

        Assert.Equal(LoggedItemStatus.Completed, items[0].Status);
        Assert.Equal(LoggedItemStatus.Skipped, items[1].Status); // intentional skip stays Skipped
        Assert.Equal(LoggedItemStatus.Missed, items[2].Status);  // no-show becomes Missed
    }

    [Fact]
    public void Close_finalizes_status_adherence_and_raises_event()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1), Planned(2) });
        log.Items.First().Complete(null); // 1 of 2 → 50%

        log.Close();

        Assert.False(log.IsOpen);
        Assert.Equal(DailyLogStatus.Closed, log.Status);
        Assert.Equal(50, log.AdherencePct);
        var evt = Assert.Single(log.DomainEvents);
        var closed = Assert.IsType<DailyLogClosedEvent>(evt);
        Assert.Equal(50, closed.AdherencePct);
        Assert.Equal(1, closed.MissedCount);
    }

    [Fact]
    public void Close_is_idempotent()
    {
        var log = OpenLog();
        log.SeedPlannedItems(new[] { Planned(1) });
        log.Close();
        log.ClearDomainEvents();

        log.Close(); // second close must be a no-op

        Assert.Equal(DailyLogStatus.Closed, log.Status);
        Assert.Empty(log.DomainEvents);
    }
}
