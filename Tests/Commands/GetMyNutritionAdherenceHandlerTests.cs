using BuildingBlocks.Shared.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Queries;
using Modules.NutritionModule.Application.Queries.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The nutrition-plan adherence trend (api/me/progress/nutrition-adherence, Phase 3), fully mocked — no
/// database. Pins the frozen rules from API-CONTRACTS §5: per-day adherence over PLANNED days only (ad-hoc
/// self-logged days excluded); the current-local-week average; HasPlan=false / empty / null-avg for a user
/// who never had a plan; and self-scoping (the repository is only ever asked for the caller's own id). Days
/// are TIME-RELATIVE to UtcNow (no calendar time-bomb). Planned logs are fed through QueryOwnAcrossGyms via
/// the in-memory <see cref="TestAsyncEnumerable{T}"/>; LocalDate/Status are seeded by reflection (the
/// DailyNutritionLog factory stamps its own).
/// </summary>
public sealed class GetMyNutritionAdherenceHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // The handler anchors "this week" to DateTimeOffset.UtcNow in the caller's zone (tests run with "UTC").
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
    private static readonly DateOnly ThisMonday = MondayOf(Today);

    private static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    // ── handler wiring ──

    private static GetMyNutritionAdherenceHandler CreateSut(Guid userId, IEnumerable<DailyNutritionLog> logs)
    {
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.QueryOwnAcrossGyms(userId).Returns(new TestAsyncEnumerable<DailyNutritionLog>(logs));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        return new GetMyNutritionAdherenceHandler(repo, currentUser);
    }

    // ── seeding (private setters ⇒ reflection; items seeded through the domain) ──

    /// <summary>A planned (FromAssignment) day on <paramref name="localDate"/> with <paramref name="planned"/>
    /// planned items, of which <paramref name="completed"/> are marked Completed. Left Open unless
    /// <paramref name="close"/> (then still-Planned items become Missed and AdherencePct is finalized).</summary>
    private static DailyNutritionLog PlannedDay(
        Guid traineeId, DateOnly localDate, int planned, int completed, bool close = false)
    {
        var log = DailyNutritionLog.Open(
            traineeId, Tenant, localDate, "UTC", NutritionSource.FromAssignment, Guid.NewGuid(), null);

        log.SeedPlannedItems(Enumerable.Range(0, planned).Select(i => PlannedItem(i)));

        var items = log.Items.ToList();
        for (var i = 0; i < completed && i < items.Count; i++)
            items[i].Complete(null);

        if (close)
            log.Close();

        return log;
    }

    /// <summary>An ad-hoc (self-logged, plan-less) day — must NOT count toward the adherence trend.</summary>
    private static DailyNutritionLog AdhocDay(Guid traineeId, DateOnly localDate)
        => DailyNutritionLog.OpenSelfLogged(traineeId, Tenant, localDate, "UTC");

    private static LoggedItemData PlannedItem(int order)
        => new(
            PlanMealItemId: Guid.NewGuid(),   // non-null ⇒ IsPlanned
            MealName: "Breakfast",
            ScheduledTime: null,
            Order: order,
            FoodId: Guid.NewGuid(),
            Kind: "Food",
            FoodNameSnapshot: "Oats",
            ServingLabelSnapshot: "1 bowl",
            Quantity: 1m,
            EnergyKcal: 300m, ProteinG: 10m, CarbsG: 50m, FatG: 6m, FiberG: 8m);

    private static async Task<Modules.NutritionModule.Application.DTOs.NutritionAdherenceDto> Run(
        Guid userId, IEnumerable<DailyNutritionLog> logs)
    {
        var result = await CreateSut(userId, logs).Handle(
            new GetMyNutritionAdherenceQuery(null, null), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    // ── per-day AdherencePct ──

    [Fact]
    public async Task Computes_per_day_adherence_for_each_planned_day_in_window()
    {
        var userId = Guid.NewGuid();
        // An OPEN day this week: 4 planned, 2 completed ⇒ 50%. A CLOSED day last week: 4 planned, 4 completed
        // ⇒ 100% (finalized AdherencePct). Two distinct local days, ordered ascending in the output.
        var openDay = PlannedDay(userId, ThisMonday, planned: 4, completed: 2);
        var closedDay = PlannedDay(userId, ThisMonday.AddDays(-7), planned: 4, completed: 4, close: true);

        var dto = await Run(userId, new[] { closedDay, openDay });

        Assert.True(dto.HasPlan);
        Assert.Equal(2, dto.Days.Count);
        Assert.Equal(ThisMonday.AddDays(-7), dto.Days[0].LocalDate);   // oldest first
        Assert.Equal(100, dto.Days[0].AdherencePct);
        Assert.Equal(4, dto.Days[0].PlannedCount);
        Assert.Equal(4, dto.Days[0].CompletedCount);
        Assert.Equal(ThisMonday, dto.Days[1].LocalDate);
        Assert.Equal(50, dto.Days[1].AdherencePct);                    // live recompute on the open day
        Assert.Equal(4, dto.Days[1].PlannedCount);
        Assert.Equal(2, dto.Days[1].CompletedCount);
    }

    [Fact]
    public async Task Excludes_adhoc_self_logged_days_from_the_trend()
    {
        var userId = Guid.NewGuid();
        // One real planned day + one ad-hoc day on a different date. The ad-hoc day (100% by convention) must
        // not appear — it has no plan to adhere to and would inflate the trend.
        var planned = PlannedDay(userId, ThisMonday, planned: 2, completed: 1);
        var adhoc = AdhocDay(userId, ThisMonday.AddDays(1));

        var dto = await Run(userId, new[] { planned, adhoc });

        Assert.True(dto.HasPlan);
        var day = Assert.Single(dto.Days);
        Assert.Equal(ThisMonday, day.LocalDate);
        Assert.Equal(50, day.AdherencePct);
    }

    // ── current-week average ──

    [Fact]
    public async Task CurrentWeekAvgPct_is_the_mean_over_this_local_weeks_planned_days()
    {
        var userId = Guid.NewGuid();
        // This week: Monday 100%, Tuesday 50% ⇒ mean 75. Last week: a 0% day that must NOT drag the average.
        var monday = PlannedDay(userId, ThisMonday, planned: 2, completed: 2);          // 100
        var tuesday = PlannedDay(userId, ThisMonday.AddDays(1), planned: 2, completed: 1); // 50
        var lastWeek = PlannedDay(userId, ThisMonday.AddDays(-7), planned: 4, completed: 0); // 0, prior week

        // Guard: only seed days that are not in the future (Today could be Monday itself).
        var seed = new[] { monday, tuesday, lastWeek }
            .Where(d => d.LocalDate <= Today)
            .ToList();

        var dto = await Run(userId, seed);

        Assert.True(dto.HasPlan);
        // The average covers only days from this Monday through today; the prior-week 0% is excluded.
        var thisWeek = dto.Days.Where(d => d.LocalDate >= ThisMonday && d.LocalDate <= Today).ToList();
        var expected = (int)System.Math.Round(thisWeek.Average(d => (double)d.AdherencePct),
            System.MidpointRounding.AwayFromZero);
        Assert.Equal(expected, dto.CurrentWeekAvgPct);
        Assert.DoesNotContain(thisWeek, d => d.AdherencePct == 0); // the prior-week 0% never entered the mean
    }

    [Fact]
    public async Task CurrentWeekAvgPct_is_null_when_no_planned_day_falls_in_the_current_week()
    {
        var userId = Guid.NewGuid();
        // Only a prior-week planned day — HasPlan stays true and Days is non-empty, but the current week is
        // empty, so the average is null (not 0).
        var priorWeek = PlannedDay(userId, ThisMonday.AddDays(-7), planned: 2, completed: 1);

        var dto = await Run(userId, new[] { priorWeek });

        Assert.True(dto.HasPlan);
        Assert.Single(dto.Days);
        Assert.Null(dto.CurrentWeekAvgPct);
    }

    // ── HasPlan=false for a never-planned user ──

    [Fact]
    public async Task HasPlan_false_with_empty_days_and_null_avg_when_the_user_never_had_a_plan()
    {
        var userId = Guid.NewGuid();
        // Only ad-hoc days ever — the user has never been assigned a nutrition plan. The empty-invite shape.
        var dto = await Run(userId, new[] { AdhocDay(userId, ThisMonday), AdhocDay(userId, ThisMonday.AddDays(-3)) });

        Assert.False(dto.HasPlan);
        Assert.Empty(dto.Days);
        Assert.Null(dto.CurrentWeekAvgPct);
    }

    [Fact]
    public async Task HasPlan_false_for_a_brand_new_user_with_no_logs_at_all()
    {
        var dto = await Run(Guid.NewGuid(), Array.Empty<DailyNutritionLog>());

        Assert.False(dto.HasPlan);
        Assert.Empty(dto.Days);
        Assert.Null(dto.CurrentWeekAvgPct);
    }

    // ── self-scope / IDOR ──

    [Fact]
    public async Task Read_only_queries_the_callers_own_logs()
    {
        var userA = Guid.NewGuid();
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.QueryOwnAcrossGyms(userA).Returns(
            new TestAsyncEnumerable<DailyNutritionLog>(new[] { PlannedDay(userA, ThisMonday, 2, 1) }));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userA);
        currentUser.TimeZoneId.Returns("UTC");
        var sut = new GetMyNutritionAdherenceHandler(repo, currentUser);

        var result = await sut.Handle(new GetMyNutritionAdherenceQuery(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The repository is only ever asked for the caller's own id — user B's logs are unreachable.
        repo.Received(1).QueryOwnAcrossGyms(userA);
        repo.DidNotReceive().QueryOwnAcrossGyms(Arg.Is<Guid>(id => id != userA));
    }

    // ── explicit window ──

    [Fact]
    public async Task An_explicit_range_bounds_the_days_returned()
    {
        var userId = Guid.NewGuid();
        var inRange = PlannedDay(userId, Today.AddDays(-2), planned: 2, completed: 2);
        var outOfRange = PlannedDay(userId, Today.AddDays(-30), planned: 2, completed: 0);

        var sut = CreateSut(userId, new[] { inRange, outOfRange });
        // Only the last 5 days.
        var result = await sut.Handle(
            new GetMyNutritionAdherenceQuery(Today.AddDays(-5), Today), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var day = Assert.Single(result.Value!.Days);
        Assert.Equal(Today.AddDays(-2), day.LocalDate);
    }
}
