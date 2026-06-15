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
        => CreateSut(userId, logs, Array.Empty<NutritionPlanAssignment>());

    /// <summary>
    /// Wires the handler over in-memory logs and the assignments that govern them. The TargetKcal redaction reads
    /// each day's governing assignment's <c>HideMacroTargets</c> via <c>QueryOwnAcrossGyms</c>, so tests that
    /// exercise hiding seed a matching <see cref="NutritionPlanAssignment"/> here.
    /// </summary>
    private static GetMyNutritionAdherenceHandler CreateSut(
        Guid userId, IEnumerable<DailyNutritionLog> logs, IEnumerable<NutritionPlanAssignment> assignments)
    {
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.QueryOwnAcrossGyms(userId).Returns(new TestAsyncEnumerable<DailyNutritionLog>(logs));

        var assignmentRepo = Substitute.For<INutritionPlanAssignmentRepository>();
        assignmentRepo.QueryOwnAcrossGyms(userId)
            .Returns(new TestAsyncEnumerable<NutritionPlanAssignment>(assignments));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        return new GetMyNutritionAdherenceHandler(repo, assignmentRepo, currentUser);
    }

    // ── seeding (private setters ⇒ reflection; items seeded through the domain) ──

    /// <summary>A planned (FromAssignment) day on <paramref name="localDate"/> with <paramref name="planned"/>
    /// planned items, of which <paramref name="completed"/> are marked Completed. Left Open unless
    /// <paramref name="close"/> (then still-Planned items become Missed and AdherencePct is finalized).</summary>
    private static DailyNutritionLog PlannedDay(
        Guid traineeId, DateOnly localDate, int planned, int completed, bool close = false,
        Guid? assignmentId = null)
    {
        var log = DailyNutritionLog.Open(
            traineeId, Tenant, localDate, "UTC", NutritionSource.FromAssignment,
            assignmentId ?? Guid.NewGuid(), null);

        log.SeedPlannedItems(Enumerable.Range(0, planned).Select(i => PlannedItem(i)));

        var items = log.Items.ToList();
        for (var i = 0; i < completed && i < items.Count; i++)
            items[i].Complete(null);

        if (close)
            log.Close();

        return log;
    }

    /// <summary>An ad-hoc (self-logged, plan-less) day — must NOT count toward the adherence trend. By default it
    /// carries <paramref name="adhocItems"/> logged items (each created already Completed via the domain), so it
    /// registers as a LOGGED day for the D15 tracking signal while still never appearing in Days.</summary>
    private static DailyNutritionLog AdhocDay(Guid traineeId, DateOnly localDate, int adhocItems = 1)
    {
        var log = DailyNutritionLog.OpenSelfLogged(traineeId, Tenant, localDate, "UTC");
        for (var i = 0; i < adhocItems; i++)
            log.AddAdhocItem(AdhocItem(i), note: null);
        return log;
    }

    private static LoggedItemData AdhocItem(int order)
        => new(
            PlanMealItemId: null,             // null ⇒ ad-hoc / off-plan
            MealName: "Snack",
            ScheduledTime: null,
            Order: order,
            FoodId: Guid.NewGuid(),
            Kind: "Food",
            FoodNameSnapshot: "Banana",
            ServingLabelSnapshot: "1 medium",
            Quantity: 1m,
            EnergyKcal: 90m, ProteinG: 1m, CarbsG: 23m, FatG: 0m, FiberG: 3m);

    /// <summary>A governing assignment with a chosen <paramref name="id"/> (so it matches a day's
    /// <c>NutritionPlanAssignmentId</c>) and the given <paramref name="hideMacroTargets"/>. The factory
    /// self-generates an Id, so we overwrite it via the AggregateRoot base setter (reflection).</summary>
    private static NutritionPlanAssignment AssignmentWithId(Guid id, Guid traineeId, bool hideMacroTargets)
    {
        var assignment = NutritionPlanAssignment.Create(
            tenantId: Tenant,
            createdBy: Guid.NewGuid(),
            traineeId: traineeId,
            planId: Guid.NewGuid(),
            planVersion: 1,
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            visibilityMode: NutritionVisibilityMode.Full,
            hideMacroTargets: hideMacroTargets,
            disableTraineeEditing: false,
            snapshotJson: null);

        typeof(NutritionPlanAssignment)
            .GetProperty(nameof(NutritionPlanAssignment.Id))!
            .SetValue(assignment, id);
        return assignment;
    }

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

    private static Task<Modules.NutritionModule.Application.DTOs.NutritionAdherenceDto> Run(
        Guid userId, IEnumerable<DailyNutritionLog> logs)
        => Run(userId, logs, Array.Empty<NutritionPlanAssignment>());

    private static async Task<Modules.NutritionModule.Application.DTOs.NutritionAdherenceDto> Run(
        Guid userId, IEnumerable<DailyNutritionLog> logs, IEnumerable<NutritionPlanAssignment> assignments)
    {
        var result = await CreateSut(userId, logs, assignments).Handle(
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

    // ── D15: ad-hoc logging is COUNTED as a separate tracking signal (never folded into the % ) ──

    [Fact]
    public async Task Self_logged_only_user_has_no_plan_yet_logged_days_are_counted()
    {
        var userId = Guid.NewGuid();
        // Two ad-hoc days THIS week (each with a logged item) + one ad-hoc day LAST week. The user has never had
        // a plan: HasPlan=false, empty Days, null avg — yet the tracking signals recognize the effort.
        var seed = new[]
            {
                AdhocDay(userId, ThisMonday),
                AdhocDay(userId, ThisMonday.AddDays(1)),
                AdhocDay(userId, ThisMonday.AddDays(-7)),
            }
            .Where(d => d.LocalDate <= Today) // Today could be Monday itself
            .ToList();
        var expectedThisWeek = seed.Count(d => d.LocalDate >= ThisMonday && d.LocalDate <= Today);

        var dto = await Run(userId, seed);

        Assert.False(dto.HasPlan);
        Assert.Empty(dto.Days);
        Assert.Null(dto.CurrentWeekAvgPct);
        // The adherence % stays honest (no inflation), but the ad-hoc effort is surfaced:
        Assert.Equal(expectedThisWeek, dto.LoggedDaysThisWeek);
        Assert.True(dto.HasAnyLogging);
    }

    [Fact]
    public async Task An_empty_self_logged_day_with_no_items_is_not_a_logged_day()
    {
        var userId = Guid.NewGuid();
        // A touched-but-empty ad-hoc day (snapshot-on-touch with nothing logged) must NOT count as logged.
        var dto = await Run(userId, new[] { AdhocDay(userId, ThisMonday, adhocItems: 0) });

        Assert.False(dto.HasPlan);
        Assert.Equal(0, dto.LoggedDaysThisWeek);
        Assert.False(dto.HasAnyLogging);
    }

    [Fact]
    public async Task Plan_user_adherence_is_unchanged_and_logged_days_count_completed_days_any_source()
    {
        var userId = Guid.NewGuid();
        // A planned day this week with a completed item (counts as both an adherence day AND a logged day) + an
        // ad-hoc day this week (logged, but never in the trend). Adherence output is exactly as before D15.
        var planned = PlannedDay(userId, ThisMonday, planned: 2, completed: 1);
        var seed = new[] { planned, AdhocDay(userId, ThisMonday.AddDays(1)) }
            .Where(d => d.LocalDate <= Today)
            .ToList();
        var expectedThisWeek = seed.Count(d => d.LocalDate >= ThisMonday && d.LocalDate <= Today);

        var dto = await Run(userId, seed);

        // Adherence trend unchanged: only the planned day, at its real 50%.
        Assert.True(dto.HasPlan);
        var day = Assert.Single(dto.Days);
        Assert.Equal(ThisMonday, day.LocalDate);
        Assert.Equal(50, day.AdherencePct);
        Assert.Equal(50, dto.CurrentWeekAvgPct);
        // Logged-days counts BOTH the completed planned day and the ad-hoc day.
        Assert.Equal(expectedThisWeek, dto.LoggedDaysThisWeek);
        Assert.True(dto.HasAnyLogging);
    }

    [Fact]
    public async Task A_planned_day_with_nothing_completed_is_not_yet_a_logged_day()
    {
        var userId = Guid.NewGuid();
        // A planned day this week whose items are all still Planned (nothing ticked) is NOT a logged day —
        // HasLoggedItem needs a Completed/Substituted item. HasPlan is still true (it's a planned day).
        var dto = await Run(userId, new[] { PlannedDay(userId, ThisMonday, planned: 2, completed: 0) });

        Assert.True(dto.HasPlan);
        Assert.Equal(0, dto.LoggedDaysThisWeek);
        Assert.False(dto.HasAnyLogging);
    }

    [Fact]
    public async Task Logged_days_this_week_ignores_prior_week_logging()
    {
        var userId = Guid.NewGuid();
        // Only a PRIOR-week ad-hoc day. HasAnyLogging is true (ever logged), but the current week is empty.
        var priorWeek = AdhocDay(userId, ThisMonday.AddDays(-7));

        var dto = await Run(userId, new[] { priorWeek });

        Assert.False(dto.HasPlan);
        Assert.Equal(0, dto.LoggedDaysThisWeek);
        Assert.True(dto.HasAnyLogging);
    }

    // ── per-day calorie totals (consumedKcal all-source, targetKcal plan-only, honesty gate) ──

    [Fact]
    public async Task ConsumedKcal_counts_adhoc_items_logged_on_a_planned_day()
    {
        var userId = Guid.NewGuid();
        // A planned day this week: 2 planned items (300 kcal each), 1 completed ⇒ consumed includes that 1
        // completed planned item (300). Adding an ad-hoc item (90 kcal, created Completed) lifts consumed to 390.
        var day = PlannedDay(userId, ThisMonday, planned: 2, completed: 1);
        day.AddAdhocItem(AdhocItem(99), note: null);

        var dto = await Run(userId, new[] { day });

        var d = Assert.Single(dto.Days);
        // Consumed = 1 completed planned (300) + 1 ad-hoc (90), ALL sources.
        Assert.Equal(390, d.ConsumedKcal);
        // Target = sum over the 2 PLANNED items only (2 × 300), ad-hoc never counted toward the target.
        Assert.Equal(600, d.TargetKcal);
        // AdherencePct stays plan-only and unchanged: 1 of 2 planned completed ⇒ 50%.
        Assert.Equal(50, d.AdherencePct);
    }

    [Fact]
    public async Task TargetKcal_is_null_when_the_governing_assignment_hides_macro_targets()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var day = PlannedDay(userId, ThisMonday, planned: 2, completed: 2, assignmentId: assignmentId);
        var hiding = AssignmentWithId(assignmentId, userId, hideMacroTargets: true);

        var dto = await Run(userId, new[] { day }, new[] { hiding });

        var d = Assert.Single(dto.Days);
        // Target redacted to null (never fabricated) — but consumed (the trainee's own logged energy) stays.
        Assert.Null(d.TargetKcal);
        Assert.Equal(600, d.ConsumedKcal); // 2 completed planned × 300
        Assert.Equal(100, d.AdherencePct); // adherence unchanged by hiding
    }

    [Fact]
    public async Task ConsumedKcal_and_TargetKcal_are_both_correct_on_a_mixed_plan_and_adhoc_day()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        // 3 planned (300 each = 900 target), 2 completed (600), plus 2 ad-hoc items (90 each = 180).
        var day = PlannedDay(userId, ThisMonday, planned: 3, completed: 2, assignmentId: assignmentId);
        day.AddAdhocItem(AdhocItem(101), note: null);
        day.AddAdhocItem(AdhocItem(102), note: null);
        var visible = AssignmentWithId(assignmentId, userId, hideMacroTargets: false);

        var dto = await Run(userId, new[] { day }, new[] { visible });

        var d = Assert.Single(dto.Days);
        Assert.Equal(900, d.TargetKcal);                 // 3 planned × 300, plan-only
        Assert.Equal(2 * 300 + 2 * 90, d.ConsumedKcal);  // 2 completed planned + 2 ad-hoc = 780
        Assert.Equal(67, d.AdherencePct);                // 2/3 ⇒ 66.67 → 67 (unchanged rounding)
    }

    // ── self-scope / IDOR ──

    [Fact]
    public async Task Read_only_queries_the_callers_own_logs()
    {
        var userA = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var repo = Substitute.For<IDailyNutritionLogRepository>();
        repo.QueryOwnAcrossGyms(userA).Returns(
            new TestAsyncEnumerable<DailyNutritionLog>(
                new[] { PlannedDay(userA, ThisMonday, 2, 1, assignmentId: assignmentId) }));

        var assignmentRepo = Substitute.For<INutritionPlanAssignmentRepository>();
        assignmentRepo.QueryOwnAcrossGyms(userA).Returns(
            new TestAsyncEnumerable<NutritionPlanAssignment>(
                new[] { AssignmentWithId(assignmentId, userA, hideMacroTargets: false) }));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userA);
        currentUser.TimeZoneId.Returns("UTC");
        var sut = new GetMyNutritionAdherenceHandler(repo, assignmentRepo, currentUser);

        var result = await sut.Handle(new GetMyNutritionAdherenceQuery(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Both repositories are only ever asked for the caller's own id — another user's data is unreachable.
        repo.Received(1).QueryOwnAcrossGyms(userA);
        repo.DidNotReceive().QueryOwnAcrossGyms(Arg.Is<Guid>(id => id != userA));
        assignmentRepo.DidNotReceive().QueryOwnAcrossGyms(Arg.Is<Guid>(id => id != userA));
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
