using Modules.FoodModule.Application.Commands;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Queries;
using Modules.NutritionModule.Entities;
using Modules.WorkoutSessionModule.Application.Queries;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Integration coverage for the imperative-authorization seam: the workout-log
/// read endpoints are exempt from declarative <c>ITenantAuthorizedRequest</c> gating and instead do
/// row-level checks inside their handlers via <c>ResourceAccessGuard</c>. These tests drive the real
/// MediatR pipeline + EF global filters against a seeded two-trainee tenant and a second tenant, and
/// specifically attempt cross-trainee and cross-tenant reads through those exempt endpoints.
///
/// Covers IntegrationTargets items #1 (tenant isolation on reads) and #2 (ListSessions scoping S3).
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CrossTraineeAccessTests(PostgresFixture fixture)
{
    private static ListSessionsQuery ListFor(Guid? traineeId) =>
        new(traineeId, From: null, To: null, Status: null, PlanAssignmentId: null, Page: 1, PageSize: 20);

    [SkippableFact]
    public async Task Client_listing_sessions_sees_only_their_own()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(ListFor(traineeId: null));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionAId);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
    }

    [SkippableFact]
    public async Task Client_cannot_read_another_trainee_by_supplying_TraineeId()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        // A Client lacks WorkoutLogViewAll, so the handler ignores the supplied TraineeId and scopes to
        // the caller — the IDOR attempt yields the caller's own (empty-of-B) list, never B's session.
        var result = await fixture.SendAsync(ListFor(traineeId: fixture.ClientBId));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!.Items, s => s.Id == fixture.SessionBId);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientAId, s.TraineeId));
    }

    [SkippableFact]
    public async Task Client_cannot_read_another_trainees_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionBId));

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [SkippableFact]
    public async Task Client_can_read_their_own_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.SessionAId, result.Value!.Id);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task Owner_with_ViewAll_can_read_a_trainees_session_by_id()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.ClientAId, result.Value!.TraineeId);
    }

    [SkippableFact]
    public async Task Owner_with_ViewAll_can_list_a_specific_trainees_sessions()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);

        var result = await fixture.SendAsync(ListFor(traineeId: fixture.ClientBId));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Items, s => s.Id == fixture.SessionBId);
        Assert.All(result.Value!.Items, s => Assert.Equal(fixture.ClientBId, s.TraineeId));
    }

    [SkippableFact]
    public async Task Owner_of_another_tenant_cannot_read_a_session_via_global_filter()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        // Same row, different tenant: the EF tenant filter hides it, so the handler 404s before any
        // row-level permission check even runs.
        fixture.Principal.Become(fixture.OtherOwnerId, fixture.OtherTenantId);

        var result = await fixture.SendAsync(new GetSessionByIdQuery(fixture.SessionAId));

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    // ── Nutrition: same imperative-authorization seam as the workout reads above ──

    /// <summary>Seeds a (plan → assignment → touched day) nutrition vertical for a trainee and returns the
    /// id of their seeded planned item for the given date.</summary>
    private async Task<Guid> SeedNutritionDayFor(Guid traineeId, DateOnly day, string foodName)
    {
        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId, isAdmin: true);
        var food = await fixture.SendAsync(new CreateFoodCommand(
            new FoodInput(foodName, "Food", "1 serving", 100m, 100m, 5m, 10m, 2m, 1m, Brand: null)));
        Assert.True(food.IsSuccess);

        fixture.Principal.Become(fixture.OwnerId, fixture.TenantId);
        var plan = await fixture.SendAsync(new CreateNutritionPlanCommand($"Plan {foodName}", null));
        Assert.True(plan.IsSuccess);
        var version = await fixture.SendAsync(new ReplaceNutritionPlanStructureCommand(
            plan.Value, $"Plan {foodName}", null,
            new[]
            {
                new NutritionPlanMealInput("Breakfast", 1, new TimeOnly(8, 0), DayApplicability.EveryDay,
                    new[] { new NutritionPlanItemInput(food.Value, 1, 1m) })
            }));
        Assert.True(version.IsSuccess);
        Assert.True((await fixture.SendAsync(new PublishNutritionPlanCommand(version.Value))).IsSuccess);
        var assign = await fixture.SendAsync(new CreateNutritionAssignmentCommand(
            traineeId, version.Value, day, EndDate: null,
            NutritionVisibilityMode.Full, HideMacroTargets: false, DisableTraineeEditing: false));
        Assert.True(assign.IsSuccess);

        // Trainee touches the day → snapshot-on-touch seeds the planned item.
        fixture.Principal.Become(traineeId, fixture.TenantId);
        var today = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(today.IsSuccess);
        return today.Value!.Meals.Single().Items.Single().Id;
    }

    [SkippableFact]
    public async Task Client_cannot_set_status_on_another_trainees_nutrition_item()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 3, 25);
        var itemBId = await SeedNutritionDayFor(fixture.ClientBId, day, "CrossNutri Eggs");
        // Give A their own day on the same date so the attack exercises the item lookup, not just
        // the "no log for that date" path.
        await SeedNutritionDayFor(fixture.ClientAId, day, "CrossNutri Toast");

        // A tries to complete (and skip) B's item — the self-scoped day lookup never surfaces B's row.
        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);
        var complete = await fixture.SendAsync(
            new SetNutritionItemStatusCommand(day, itemBId, LoggedItemStatus.Completed, "not mine"));
        var skip = await fixture.SendAsync(
            new SetNutritionItemStatusCommand(day, itemBId, LoggedItemStatus.Skipped, null));

        Assert.True(complete.IsFailure);
        Assert.Equal("NotFound", complete.Error.Code);
        Assert.True(skip.IsFailure);
        Assert.Equal("NotFound", skip.Error.Code);

        // B's item is untouched.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
        var dayB = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(dayB.IsSuccess);
        var itemB = Assert.Single(dayB.Value!.Meals.Single().Items, i => i.Id == itemBId);
        Assert.Equal(LoggedItemStatus.Planned, itemB.Status);
    }

    [SkippableFact]
    public async Task Self_scoped_nutrition_reads_only_return_the_callers_own_rows()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var day = new DateOnly(2026, 3, 26);
        var itemBId = await SeedNutritionDayFor(fixture.ClientBId, day, "SelfScope Yoghurt");
        await SeedNutritionDayFor(fixture.ClientAId, day, "SelfScope Apple");

        fixture.Principal.Become(fixture.ClientAId, fixture.TenantId);

        // Today: A's day never contains B's item.
        var todayA = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(todayA.IsSuccess);
        Assert.DoesNotContain(todayA.Value!.Meals.SelectMany(m => m.Items), i => i.Id == itemBId);
        Assert.Contains(todayA.Value.Meals.SelectMany(m => m.Items), i => i.FoodName == "SelfScope Apple");

        // History/day list: the row A sees for that date is A's own day, not B's.
        var historyA = await fixture.SendAsync(new GetMyNutritionHistoryQuery(day, day, 1, 10));
        Assert.True(historyA.IsSuccess);
        var historyDay = Assert.Single(historyA.Value!.Items, d => d.LocalDate == day);
        Assert.Equal(todayA.Value.Id, historyDay.Id);

        // And B still sees exactly their own item.
        fixture.Principal.Become(fixture.ClientBId, fixture.TenantId);
        var todayB = await fixture.SendAsync(new GetMyNutritionTodayQuery(day, "UTC"));
        Assert.True(todayB.IsSuccess);
        var itemB = Assert.Single(todayB.Value!.Meals.SelectMany(m => m.Items));
        Assert.Equal(itemBId, itemB.Id);
        Assert.NotEqual(todayA.Value.Id, todayB.Value.Id);
    }
}
