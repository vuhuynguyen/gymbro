using BuildingBlocks.Shared.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.FoodModule.Application.Commands;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Queries;
using Modules.NutritionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// End-to-end nutrition vertical against a real Postgres: admin seeds a global food, a coach authors +
/// assigns a plan, and the trainee's self-scoped <c>api/me/nutrition</c> day is created by snapshot-on-touch,
/// logged completion-first, and rolled up into adherence — read back through both the trainee history and the
/// coach adherence list (which exercises the SQL count projections). Skips when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NutritionFlowTests(PostgresFixture fixture)
{
    private static readonly DateOnly Day = new(2026, 3, 15);

    [SkippableFact]
    public async Task Snapshot_on_touch_logging_and_adherence_round_trip()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // 1. Admin creates a global catalog food.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var foodResult = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Oats", "Food", "1 bowl", 60m, 300m, 10m, 50m, 6m, 8m, Brand: null)));
        Assert.True(foodResult.IsSuccess);
        var foodId = foodResult.Value;

        // 2. Coach authors + versions a nutrition plan, then assigns it to ClientA.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var planResult = await fixture.SendAsync(new CreateNutritionPlanCommand("Cut Plan", null));
        Assert.True(planResult.IsSuccess);

        var structureResult = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
            planResult.Value, "Cut Plan", "Phase 1",
            new[]
            {
                new NutritionPlanMealInput("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                    new[] { new NutritionPlanItemInput(foodId, 1, 1m) })
            }));
        Assert.True(structureResult.IsSuccess);
        var planVersionId = structureResult.Value;

        // Publishing the draft is what makes it assignable (plain edits never advance the version).
        Assert.True((await fixture.SendAsync(new PublishNutritionPlanCommand(planVersionId))).IsSuccess);

        var assignResult = await fixture.SendAsync(new CreateNutritionAssignmentCommand(
            fixture.ClientAId, planVersionId, Day, EndDate: null,
            NutritionVisibilityMode.Full, HideMacroTargets: false, DisableTraineeEditing: false));
        Assert.True(assignResult.IsSuccess);

        // 3. Trainee opens today → snapshot-on-touch creates + seeds the day from the assignment snapshot.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var today = await fixture.SendAsync(new GetMyNutritionTodayQuery(Day, "UTC"));
        Assert.True(today.IsSuccess);
        Assert.True(today.Value!.HasPlan);
        Assert.Equal(1, today.Value.PlannedCount);
        Assert.Equal(0, today.Value.CompletedCount);
        var meal = Assert.Single(today.Value.Meals);
        Assert.Equal("Breakfast", meal.Name);
        var item = Assert.Single(meal.Items);
        Assert.Equal("Oats", item.FoodName);
        Assert.Equal(LoggedItemStatus.Planned, item.Status);

        // 4. Completion-first: tap "ate it".
        var setStatus = await fixture.SendAsync(
            new SetNutritionItemStatusCommand(Day, item.Id, LoggedItemStatus.Completed, "ate it"));
        Assert.True(setStatus.IsSuccess);

        // 5. Today reflects the completion + 100% adherence (no second day row created).
        var after = await fixture.SendAsync(new GetMyNutritionTodayQuery(Day, "UTC"));
        Assert.True(after.IsSuccess);
        Assert.Equal(today.Value.Id, after.Value!.Id); // same day, not a duplicate
        Assert.Equal(1, after.Value.CompletedCount);
        Assert.Equal(100, after.Value.AdherencePct);
        Assert.Equal(LoggedItemStatus.Completed, after.Value.Meals.Single().Items.Single().Status);

        // 6. Trainee history list (exercises the count-projection, no item rows loaded).
        var history = await fixture.SendAsync(new GetMyNutritionHistoryQuery(null, null, 1, 30));
        Assert.True(history.IsSuccess);
        var historyDay = Assert.Single(history.Value!.Items, d => d.LocalDate == Day);
        Assert.Equal(100, historyDay.AdherencePct);
        Assert.Equal(1, historyDay.PlannedCount);
        Assert.Equal(1, historyDay.CompletedCount);

        // 7. Coach adherence list for the client (tenant-scoped, count-projection).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var coachView = await fixture.SendAsync(
            new ListTraineeNutritionDaysQuery(fixture.ClientAId, null, null, 1, 30));
        Assert.True(coachView.IsSuccess);
        var coachDay = Assert.Single(coachView.Value!.Items, d => d.LocalDate == Day);
        Assert.Equal(100, coachDay.AdherencePct);
    }

    [SkippableFact]
    public async Task Ad_hoc_logging_is_idempotent_by_client_item_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 3, 28);
        var clientItemId = Guid.NewGuid();

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var foodResult = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Idempotency Banana", "Food", "1 medium", 120m, 105m, 1m, 27m, 0m, 3m, Brand: null)));
        Assert.True(foodResult.IsSuccess);
        var foodId = foodResult.Value;

        // Self-log (Owner has no nutrition assignment, so the day is plain self-logged — deterministic regardless
        // of other tests' assignments) the SAME ad-hoc item twice with one client id: a flaky retry / offline replay.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var first = await fixture.SendAsync(new AddAdhocNutritionItemCommand(
            day, foodId, 1m, "Snack", null, ClientItemId: clientItemId));
        Assert.True(first.IsSuccess);
        var second = await fixture.SendAsync(new AddAdhocNutritionItemCommand(
            day, foodId, 1m, "Snack", null, ClientItemId: clientItemId));
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value); // replay returns the SAME item, never a duplicate

        var today = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(today.IsSuccess);
        var bananas = today.Value!.Meals.SelectMany(m => m.Items).Count(i => i.FoodName == "Idempotency Banana");
        Assert.Equal(1, bananas); // exactly one, despite two log calls
    }

    [SkippableFact]
    public async Task Assignment_snapshot_and_seeded_day_survive_a_new_plan_version()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 3, 20);

        // 1. Admin creates two global catalog foods (v1 uses the first, v2 the second).
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var foodV1 = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Snapshot Oats", "Food", "1 bowl", 60m, 300m, 10m, 50m, 6m, 8m, Brand: null)));
        Assert.True(foodV1.IsSuccess);
        var foodV2 = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Snapshot Rice", "Food", "1 cup", 150m, 200m, 4m, 45m, 0.5m, 1m, Brand: null)));
        Assert.True(foodV2.IsSuccess);

        // 2. Coach authors v1 and assigns it to ClientA.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var planResult = await fixture.SendAsync(new CreateNutritionPlanCommand("Snapshot Plan", null));
        Assert.True(planResult.IsSuccess);

        var v1 = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
            planResult.Value, "Snapshot Plan", "v1",
            new[]
            {
                new NutritionPlanMealInput("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                    new[] { new NutritionPlanItemInput(foodV1.Value, 1, 1m) })
            }));
        Assert.True(v1.IsSuccess);

        // Publish v1 so it can be assigned.
        Assert.True((await fixture.SendAsync(new PublishNutritionPlanCommand(v1.Value))).IsSuccess);

        var assign = await fixture.SendAsync(new CreateNutritionAssignmentCommand(
            fixture.ClientAId, v1.Value, day, EndDate: null,
            NutritionVisibilityMode.Full, HideMacroTargets: false, DisableTraineeEditing: false));
        Assert.True(assign.IsSuccess);

        // 3. Trainee touches the day → items seeded from the v1 assignment snapshot.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var before = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(before.IsSuccess);
        Assert.Equal("Snapshot Oats", before.Value!.Meals.Single().Items.Single().FoodName);

        // 4. Coach edits the plan (forking a new draft v2) with different meals/items (Dinner + Rice). It need
        //    not even be published — the trainee's assignment stays pinned to its v1 snapshot regardless.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var v2 = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
            v1.Value, "Snapshot Plan", "v2",
            new[]
            {
                new NutritionPlanMealInput("Dinner", 1, new TimeOnly(19, 0), DayApplicability.EveryDay,
                    new[] { new NutritionPlanItemInput(foodV2.Value, 1, 2m) })
            }));
        Assert.True(v2.IsSuccess);
        Assert.NotEqual(v1.Value, v2.Value);

        // 5. The trainee's day still reflects v1 — snapshot-on-touch is immutable under re-versioning.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var after = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(after.IsSuccess);
        Assert.Equal(before.Value.Id, after.Value!.Id); // same day row, not re-seeded
        var meal = Assert.Single(after.Value.Meals);
        Assert.Equal("Breakfast", meal.Name);
        var item = Assert.Single(meal.Items);
        Assert.Equal("Snapshot Oats", item.FoodName);
        Assert.Equal(foodV1.Value, item.FoodId);

        // 6. The assignment snapshot still points at the v1 plan version.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var assignments = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientAId, ActiveOnly: false, 1, 50));
        Assert.True(assignments.IsSuccess);
        var assignment = Assert.Single(assignments.Value!.Items, a => a.Id == assign.Value);
        Assert.Equal(v1.Value, assignment.PlanId);          // still the v1 version row
        Assert.Equal("Snapshot Plan", assignment.PlanName);
    }

    [SkippableFact]
    public async Task Metric_check_in_round_trip_is_self_scoped_and_newest_first()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 4, 10);

        // 1. ClientA checks in weight and sleep for the day (self-scoped, no tenant context).
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        Assert.True((await fixture.SendAsync(
            new LogMetricEntryCommand("weight", 82.5m, "kg", day))).IsSuccess);
        Assert.True((await fixture.SendAsync(
            new LogMetricEntryCommand("sleep", 7.5m, "h", day))).IsSuccess);
        // A later re-log of weight — the client treats the newest entry per type as "latest".
        Assert.True((await fixture.SendAsync(
            new LogMetricEntryCommand("weight", 82.1m, "kg", day))).IsSuccess);

        // 2. Read back for the date: all three entries, newest first.
        var mine = await fixture.SendAsync(new GetMyNutritionMetricsQuery(day));
        Assert.True(mine.IsSuccess);
        Assert.Equal(3, mine.Value!.Items.Count);
        Assert.True(mine.Value.Items.Zip(mine.Value.Items.Skip(1))
            .All(p => p.First.LoggedAtUtc >= p.Second.LoggedAtUtc));
        Assert.Equal(82.1m, mine.Value.Items.First(i => i.Type == "weight").Value); // latest weight wins
        Assert.Equal(7.5m, Assert.Single(mine.Value.Items, i => i.Type == "sleep").Value);
        Assert.All(mine.Value.Items, i => Assert.Equal(day, i.LocalDate));

        // 3. Another user reading the same date sees none of ClientA's entries.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
        var theirs = await fixture.SendAsync(new GetMyNutritionMetricsQuery(day));
        Assert.True(theirs.IsSuccess);
        Assert.Empty(theirs.Value!.Items);
    }

    [SkippableFact]
    public async Task Hide_macro_targets_redacts_the_trainees_planned_macros_but_never_the_coachs()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // HideMacroTargets is a filter-on-read coaching dial (the nutrition sibling of workout HideSetsReps):
        // when set, the trainee sees WHAT to eat (meal/food/serving) but not the macro TARGETS; the coach read is
        // never filtered. VisibilityMode is stored but its coarser plan-structure hiding is a later refinement,
        // so redaction today keys off the explicit HideMacroTargets flag, independent of mode.
        var modes = new (NutritionVisibilityMode Mode, bool HideMacros, DateOnly Day)[]
        {
            (NutritionVisibilityMode.Full, false, new DateOnly(2026, 5, 1)),
            (NutritionVisibilityMode.Guided, true, new DateOnly(2026, 5, 2)),
            (NutritionVisibilityMode.Blind, false, new DateOnly(2026, 5, 3)),
        };

        // Seed one catalog food.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var food = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Visibility Oats", "Food", "1 bowl", 60m, 300m, 10m, 50m, 6m, 8m, Brand: null)));
        Assert.True(food.IsSuccess);

        foreach (var (mode, hideMacros, day) in modes)
        {
            // One plan + bounded one-day assignment per mode (same trainee, non-overlapping windows).
            fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
            var plan = await fixture.SendAsync(new CreateNutritionPlanCommand($"Vis {mode}", null));
            Assert.True(plan.IsSuccess);
            var version = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
                plan.Value, $"Vis {mode}", null,
                new[]
                {
                    new NutritionPlanMealInput("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                        new[] { new NutritionPlanItemInput(food.Value, 1, 1m) })
                }));
            Assert.True(version.IsSuccess);
            Assert.True((await fixture.SendAsync(new PublishNutritionPlanCommand(version.Value))).IsSuccess);
            var assigned = await fixture.SendAsync(new CreateNutritionAssignmentCommand(
                fixture.ClientBId, version.Value, day, EndDate: day,
                mode, hideMacros, DisableTraineeEditing: false));
            Assert.True(assigned.IsSuccess);

            // Trainee day read: the planned macro TARGETS are redacted iff HideMacroTargets is set; the
            // what-to-eat fields are always shown.
            fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
            var traineeDay = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
            Assert.True(traineeDay.IsSuccess);
            var traineeItem = Assert.Single(Assert.Single(traineeDay.Value!.Meals).Items);
            Assert.Equal("Visibility Oats", traineeItem.FoodName);
            if (hideMacros)
            {
                Assert.Null(traineeItem.EnergyKcal);
                Assert.Null(traineeItem.ProteinG);
            }
            else
            {
                Assert.Equal(300m, traineeItem.EnergyKcal);
                Assert.Equal(10m, traineeItem.ProteinG);
            }

            // Coach day read is never filtered, in any mode.
            fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
            var coachDay = await fixture.SendAsync(new GetTraineeNutritionDayQuery(fixture.ClientBId, day));
            Assert.True(coachDay.IsSuccess);
            var coachItem = Assert.Single(Assert.Single(coachDay.Value!.Meals).Items);
            Assert.Equal(300m, coachItem.EnergyKcal);
            Assert.Equal(10m, coachItem.ProteinG);

            // The mode + flags are faithfully stored on the assignment (the forward-compat contract).
            var list = await fixture.SendAsync(
                new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: false, 1, 50));
            Assert.True(list.IsSuccess);
            var dto = Assert.Single(list.Value!.Items, a => a.Id == assigned.Value);
            Assert.Equal(mode, dto.VisibilityMode);
            Assert.Equal(hideMacros, dto.HideMacroTargets);
        }
    }

    [SkippableFact]
    public async Task Self_train_owner_logs_off_plan_food_without_an_assignment()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 7, 4);

        // 1. Admin seeds a global catalog food.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var food = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Self Banana", "Food", "1 medium", 120m, 105m, 1.3m, 27m, 0.4m, 3m, Brand: null)));
        Assert.True(food.IsSuccess);

        // 2. The gym OWNER (self-train, no nutrition assignment) logs an off-plan catalog food on the
        //    tenant-scoped write surface (api/nutrition/log). Become(OwnerId, TenantId) sets the active gym, so
        //    the NutritionLogCreate-gated command (Owner holds it) provisions a plan-less day stamped with that
        //    gym, not a 404.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var add = await fixture.SendAsync(new AddAdhocNutritionItemCommand(day, food.Value, 1m, "Snack", "tasty"));
        Assert.True(add.IsSuccess);

        // 3. The day reads back on the self surface, with the ad-hoc item completed.
        var today = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(today.IsSuccess);
        Assert.False(today.Value!.HasPlan); // no plan governs it
        Assert.Equal("Adhoc", today.Value.Source);
        var item = Assert.Single(Assert.Single(today.Value.Meals).Items);
        Assert.Equal("Self Banana", item.FoodName);
        Assert.Equal(LoggedItemStatus.Completed, item.Status);

        // 4. It persisted, stamped with the ACTIVE gym (from the X-Tenant-Id header), Source=Adhoc, no assignment.
        await fixture.InScopeAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IDailyNutritionLogRepository>();
            var stored = await repo.GetOwnByDateAsync(fixture.OwnerId, day, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Equal(NutritionSource.Adhoc, stored!.Source);
            Assert.Equal(fixture.TenantId, stored.TenantId);
            Assert.Null(stored.NutritionPlanAssignmentId);
            Assert.Single(stored.Items);
        });

        // 5. Marking the ad-hoc item works, and so does removing it.
        var itemId = item.Id;
        var status = await fixture.SendAsync(
            new SetNutritionItemStatusCommand(day, itemId, LoggedItemStatus.Skipped, null));
        Assert.True(status.IsSuccess);
        var remove = await fixture.SendAsync(new RemoveNutritionItemCommand(day, itemId));
        Assert.True(remove.IsSuccess);

        // 6. A coach in a DIFFERENT gym cannot see the self-train owner's day (tenant filter bounds it).
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var rivalView = await fixture.SendAsync(
            new ListTraineeNutritionDaysQuery(fixture.OwnerId, null, null, 1, 30));
        Assert.True(rivalView.IsSuccess);
        Assert.DoesNotContain(rivalView.Value!.Items, d => d.LocalDate == day);
    }

    [SkippableFact]
    public async Task Two_adhoc_adds_to_the_same_day_in_separate_scopes_both_succeed()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 7, 9);

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);

        // First add: creates the (owner, day) self-logged day and inserts one item (separate scope).
        var first = await fixture.SendAsync(new AddAdhocNutritionItemCommand(
            day, null, 1m, "Snack", null, CustomName: "Almonds", EnergyKcal: 160m));
        Assert.True(first.IsSuccess, $"first add failed: {(first.IsFailure ? first.Error.Code : "")}");

        // Second add: loads the EXISTING day in a fresh scope/DbContext and inserts a second child item.
        // This is the load-then-add path the duplicate-day race-retry does NOT cover.
        var second = await fixture.SendAsync(new AddAdhocNutritionItemCommand(
            day, null, 1m, "Snack", null, CustomName: "Walnuts", EnergyKcal: 180m));
        Assert.True(second.IsSuccess, $"second add failed: {(second.IsFailure ? second.Error.Code : "")}");

        // One day row, two items.
        await fixture.InScopeAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IDailyNutritionLogRepository>();
            var stored = await repo.GetOwnByDateAsync(fixture.OwnerId, day, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Equal(2, stored!.Items.Count);
        });
    }

    [SkippableFact]
    public async Task Adhoc_add_for_a_non_member_of_the_active_gym_is_rejected_by_authorization()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 7, 5);

        // The write surface is now tenant-scoped (ITenantAuthorizedRequest, NutritionLogCreate). A user who is
        // NOT a member of the active gym (no UserTenantRole in TenantId) has no role there, so
        // AuthorizationBehavior denies the command — a clean Result failure, not an exception/500.
        var orphanId = Guid.NewGuid();
        fixture.Principal.Become(orphanId, fixture.TenantId);

        var result = await fixture.SendAsync(
            new AddAdhocNutritionItemCommand(day, null, 1m, "Snack", null, CustomName: "Mystery bar"));

        Assert.True(result.IsFailure); // a clean Result failure (Unauthorized), not an exception/500
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [SkippableFact]
    public async Task Self_logged_day_is_one_row_stamped_with_the_active_gym_invisible_cross_gym()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 7, 6);

        // The owner logs an off-plan custom food on the tenant-scoped write surface under their own gym (gym A).
        // A nutrition day is unique per (trainee, date) globally, so it is a single row stamped with the gym
        // that was active when it was first created.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var add = await fixture.SendAsync(new AddAdhocNutritionItemCommand(
            day, null, 1m, "Snack", null, CustomName: "Almonds", EnergyKcal: 160m));
        Assert.True(add.IsSuccess);

        // Exactly one (trainee, date) day, stamped with gym A.
        await fixture.InScopeAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IDailyNutritionLogRepository>();
            var all = await repo.QueryOwnAcrossGyms(fixture.OwnerId)
                .Where(l => l.LocalDate == day)
                .ToListAsync();
            var stored = Assert.Single(all);
            Assert.Equal(fixture.TenantId, stored.TenantId); // stamped with the active gym (gym A)
            Assert.Equal(NutritionSource.Adhoc, stored.Source);
        });

        // A rival-gym owner cannot see that day — the tenant filter bounds the coach read to gym A.
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var rivalView = await fixture.SendAsync(
            new ListTraineeNutritionDaysQuery(fixture.OwnerId, null, null, 1, 30));
        Assert.True(rivalView.IsSuccess);
        Assert.DoesNotContain(rivalView.Value!.Items, d => d.LocalDate == day);
    }

    [SkippableFact]
    public async Task Assignment_lifecycle_and_plan_archive_round_trip()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var startDate = new DateOnly(2026, 9, 1);

        // 1. Admin seeds a catalog food; coach authors + versions a plan.
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var food = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput("Lifecycle Oats", "Food", "1 bowl", 60m, 300m, 10m, 50m, 6m, 8m, Brand: null)));
        Assert.True(food.IsSuccess);

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var plan = await fixture.SendAsync(new CreateNutritionPlanCommand("Lifecycle Plan", null));
        Assert.True(plan.IsSuccess);
        var version = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
            plan.Value, "Lifecycle Plan", "v1",
            new[]
            {
                new NutritionPlanMealInput("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                    new[] { new NutritionPlanItemInput(food.Value, 1, 1m) })
            }));
        Assert.True(version.IsSuccess);
        Assert.True((await fixture.SendAsync(new PublishNutritionPlanCommand(version.Value))).IsSuccess);

        // 2. Assign it to ClientB.
        var assign = await fixture.SendAsync(new CreateNutritionAssignmentCommand(
            fixture.ClientBId, version.Value, startDate, EndDate: null,
            NutritionVisibilityMode.Full, HideMacroTargets: false, DisableTraineeEditing: false));
        Assert.True(assign.IsSuccess);
        var assignmentId = assign.Value;

        // 3. EDIT the assignment (new dates + visibility + flags); the pinned version + name are kept.
        var newStart = new DateOnly(2026, 9, 15);
        var newEnd = new DateOnly(2026, 12, 31);
        var edit = await fixture.SendAsync(new UpdateNutritionAssignmentCommand(
            assignmentId, newStart, newEnd, NutritionVisibilityMode.Guided,
            HideMacroTargets: true, DisableTraineeEditing: true));
        Assert.True(edit.IsSuccess);

        var afterEdit = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: false, 1, 50));
        Assert.True(afterEdit.IsSuccess);
        var edited = Assert.Single(afterEdit.Value!.Items, a => a.Id == assignmentId);
        Assert.Equal(newStart, edited.StartDate);
        Assert.Equal(newEnd, edited.EndDate);
        Assert.Equal(NutritionVisibilityMode.Guided, edited.VisibilityMode);
        Assert.True(edited.HideMacroTargets);
        Assert.True(edited.DisableTraineeEditing);
        Assert.Equal(version.Value, edited.PlanId); // pinned version untouched
        Assert.True(edited.IsActive);

        // 4. PAUSE → drops out of the active-only list, still present unfiltered.
        Assert.True((await fixture.SendAsync(new SetNutritionAssignmentActiveCommand(assignmentId, false))).IsSuccess);
        var activeOnly = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: true, 1, 50));
        Assert.True(activeOnly.IsSuccess);
        Assert.DoesNotContain(activeOnly.Value!.Items, a => a.Id == assignmentId);
        var paused = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: false, 1, 50));
        Assert.False(Assert.Single(paused.Value!.Items, a => a.Id == assignmentId).IsActive);

        // 5. RESUME → back in the active-only list.
        Assert.True((await fixture.SendAsync(new SetNutritionAssignmentActiveCommand(assignmentId, true))).IsSuccess);
        var resumed = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: true, 1, 50));
        Assert.True(resumed.IsSuccess);
        Assert.True(Assert.Single(resumed.Value!.Items, a => a.Id == assignmentId).IsActive);

        // 6. REVOKE (soft-delete) → gone from the unfiltered list.
        Assert.True((await fixture.SendAsync(new DeleteNutritionAssignmentCommand(assignmentId))).IsSuccess);
        var afterRevoke = await fixture.SendAsync(
            new ListNutritionAssignmentsQuery(fixture.ClientBId, ActiveOnly: false, 1, 50));
        Assert.True(afterRevoke.IsSuccess);
        Assert.DoesNotContain(afterRevoke.Value!.Items, a => a.Id == assignmentId);

        // 7. ARCHIVE the plan → drops out of the default list, appears in the archived list.
        Assert.True((await fixture.SendAsync(new SetNutritionPlanArchivedCommand(version.Value, true))).IsSuccess);
        var defaultList = await fixture.SendAsync(new ListNutritionPlansQuery("Lifecycle Plan", 1, 50, Archived: false));
        Assert.True(defaultList.IsSuccess);
        Assert.DoesNotContain(defaultList.Value!.Items, p => p.TemplateId == version.Value || p.Name == "Lifecycle Plan");

        var archivedList = await fixture.SendAsync(new ListNutritionPlansQuery("Lifecycle Plan", 1, 50, Archived: true));
        Assert.True(archivedList.IsSuccess);
        var archived = Assert.Single(archivedList.Value!.Items, p => p.Name == "Lifecycle Plan");
        Assert.True(archived.IsArchived);

        // 8. UNARCHIVE → back in the default list, gone from the archived list.
        Assert.True((await fixture.SendAsync(new SetNutritionPlanArchivedCommand(version.Value, false))).IsSuccess);
        var restored = await fixture.SendAsync(new ListNutritionPlansQuery("Lifecycle Plan", 1, 50, Archived: false));
        Assert.True(restored.IsSuccess);
        var restoredPlan = Assert.Single(restored.Value!.Items, p => p.Name == "Lifecycle Plan");
        Assert.False(restoredPlan.IsArchived);
    }

    [SkippableFact]
    public async Task Coach_cannot_see_another_gyms_client_nutrition()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // The rival-gym owner asking for ClientA's days gets an empty list — the tenant filter bounds the
        // coach read to their own gym (ClientA's day is stamped with the original tenant).
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);
        var result = await fixture.SendAsync(
            new ListTraineeNutritionDaysQuery(fixture.ClientAId, null, null, 1, 30));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!.Items, d => d.LocalDate == Day);
    }
}
