using Modules.FoodModule.Application.Commands;
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
