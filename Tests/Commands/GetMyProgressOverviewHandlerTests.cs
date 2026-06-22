using System.Reflection;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The Progress-home overview math (api/me/progress/overview, Phase 1), fully mocked — no database. Pins the
/// frozen business rules from API-CONTRACTS §1: completed-only adherence; the D1 goal vs an ad-hoc null goal;
/// the e1RM honesty gate (Working + e1RM + reps ≤ 12 + Strength/Bodyweight); one MAX point per session; the
/// ≥4-session / top-3 top-lift selection; the 3-exposure stall; the PR teaser top-3 (never PrCount); and the
/// 200-with-empty-DTO new-user case. Sessions are fed through QueryOwnAcrossGyms via the in-memory
/// <see cref="TestAsyncEnumerable{T}"/>, with StartedAt/Status/sets seeded by reflection (entity factories
/// stamp UtcNow and start InProgress).
/// </summary>
public sealed class GetMyProgressOverviewHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // M1 (no time-bomb): the handler anchors "this week" to DateTimeOffset.UtcNow in the caller's zone (the
    // tests run with "UTC"). Mirror that EXACT computation here so every session date is seeded relative to
    // the current week's Monday — the suite must pass identically in any calendar week it runs in, never
    // depending on a hard-coded date. `ThisMonday` is the Monday of the UTC week containing "now".
    private static readonly DateOnly ThisMonday = MondayOfUtcWeek(DateTimeOffset.UtcNow);

    // 09:00 UTC on this week's Monday — a concrete instant inside the current week, for seeding sessions.
    private static readonly DateTimeOffset ThisMondayInstant =
        new(ThisMonday.Year, ThisMonday.Month, ThisMonday.Day, 9, 0, 0, TimeSpan.Zero);

    // PRs are now windowed by AchievedAt (the overview keeps only PRs set within the selected window), so this
    // sits on THIS week — in-window for every period. The window test below uses older instants to verify
    // out-of-window PRs are dropped.
    private static readonly DateTimeOffset NowUtc = ThisMondayInstant;

    // Monday-anchored start of the UTC week containing the instant — the same rule the handler uses.
    private static DateOnly MondayOfUtcWeek(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    // A concrete 09:00-UTC instant on the Monday `weeksAgo` weeks before the current week (0 = this week).
    private static DateTimeOffset MondayInstant(int weeksAgo)
        => ThisMondayInstant.AddDays(-7 * weeksAgo);

    // ── handler wiring ──

    private static (GetMyProgressOverviewHandler Sut, IMediator Mediator) CreateSut(
        Guid userId,
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyList<OwnActiveAssignmentDto>? activeAssignments = null,
        IReadOnlyList<PersonalRecordDto>? records = null,
        IReadOnlyDictionary<Guid, string>? muscleMap = null)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        // The handler reads QueryOwnAcrossGyms TWICE (strength points + the v2 stats read) — hand back a FRESH
        // re-enumerable each call so the second read isn't enumerating a spent sequence.
        repo.QueryOwnAcrossGyms(userId)
            .Returns(_ => new TestAsyncEnumerable<WorkoutSession>(sessions));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetOwnActiveAssignmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<OwnActiveAssignmentDto>>.Success(activeAssignments ?? []));
        mediator.Send(Arg.Any<GetMyPersonalRecordsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PersonalRecordListDto>.Success(new PersonalRecordListDto(records ?? [])));
        mediator.Send(Arg.Any<ResolveExerciseMuscleGroupsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, string>>.Success(
                muscleMap ?? new Dictionary<Guid, string>()));

        return (new GetMyProgressOverviewHandler(repo, mediator, currentUser), mediator);
    }

    private static Task<Result<ProgressOverviewDto>> Run(
        Guid userId,
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyList<OwnActiveAssignmentDto>? activeAssignments = null,
        IReadOnlyList<PersonalRecordDto>? records = null,
        int? weeks = null,
        IReadOnlyDictionary<Guid, string>? muscleMap = null)
    {
        var (sut, _) = CreateSut(userId, sessions, activeAssignments, records, muscleMap);
        return sut.Handle(new GetMyProgressOverviewQuery(weeks), CancellationToken.None);
    }

    // ── entity seeding (private setters + UtcNow stamps ⇒ reflection) ──

    private static WorkoutSession CompletedSession(
        Guid traineeId, DateTimeOffset startedAt, params PerformedExercise[] exercises)
        => SessionWithStatus(traineeId, startedAt, SessionStatus.Completed, exercises);

    private static WorkoutSession SessionWithStatus(
        Guid traineeId, DateTimeOffset startedAt, SessionStatus status, params PerformedExercise[] exercises)
    {
        var session = WorkoutSession.Start(
            traineeId, Tenant, SessionSource.Adhoc, null, null, "Lift", null, "UTC", null);
        SetProp(session, "StartedAt", startedAt);
        SetProp(session, "Status", status);
        Backing<PerformedExercise>(session, "_exercises").AddRange(exercises);
        return session;
    }

    private static PerformedExercise Exercise(
        Guid exerciseId, string name, ExerciseTrackingType trackingType, params PerformedSet[] sets)
    {
        var exercise = PerformedExercise.Create(Guid.NewGuid(), Tenant, exerciseId, null, 0, name, trackingType);
        Backing<PerformedSet>(exercise, "_sets").AddRange(sets);
        return exercise;
    }

    private static PerformedSet WorkingSet(int reps, decimal weightKg)
        => PerformedSet.Log(Guid.NewGuid(), Tenant, null, 1, PerformedSetType.Working,
            reps, weightKg, null, null, null, null, true);

    private static PerformedSet Set(PerformedSetType type, int reps, decimal weightKg, Guid? parent = null)
        => PerformedSet.Log(Guid.NewGuid(), Tenant, null, 1, type,
            reps, weightKg, null, null, null, null, true, parentSetId: parent);

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    private static List<T> Backing<T>(object target, string field)
        => (List<T>)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(target)!;

    // ── new user / empty ──

    [Fact]
    public async Task New_user_with_no_sessions_returns_empty_but_valid_overview()
    {
        var userId = Guid.NewGuid();

        var result = await Run(userId, []);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(0, dto.ThisWeek.CompletedSessions);
        Assert.Null(dto.ThisWeek.Goal);
        Assert.False(dto.ThisWeek.HasActivePlan);
        Assert.Empty(dto.TopLifts);
        Assert.Empty(dto.RecentPrs);
        Assert.Empty(dto.Consistency.Days);
        Assert.Null(dto.Consistency.ConsistencyPct);
        Assert.Equal(0, dto.Consistency.CurrentStreakWeeks);
        Assert.Equal(12, dto.Consistency.WindowWeeks);
    }

    // ── adherence: completed-only, current week ──

    [Fact]
    public async Task Adherence_counts_completed_sessions_in_the_current_week_only()
    {
        var userId = Guid.NewGuid();
        var monday = ThisMondayInstant;          // this week
        var lastWeek = MondayInstant(1).AddDays(2); // prior week (Wed of last week)

        var sut = WithSessions(userId,
            CompletedSession(userId, monday),
            CompletedSession(userId, monday.AddDays(2)),
            CompletedSession(userId, lastWeek));

        var result = await sut;

        Assert.True(result.IsSuccess);
        Assert.Equal(ThisMonday, result.Value!.ThisWeek.WeekStart);
        Assert.Equal(2, result.Value!.ThisWeek.CompletedSessions);
    }

    [Fact]
    public async Task Adherence_excludes_abandoned_and_in_progress_sessions()
    {
        var userId = Guid.NewGuid();
        var monday = ThisMondayInstant;

        var result = await Run(userId,
        [
            CompletedSession(userId, monday),
            SessionWithStatus(userId, monday.AddDays(1), SessionStatus.Abandoned),
            SessionWithStatus(userId, monday.AddDays(2), SessionStatus.InProgress),
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.ThisWeek.CompletedSessions);
    }

    // ── D1 goal ──

    [Fact]
    public async Task Goal_is_the_frequency_of_the_active_assignment_with_most_completed_sessions_this_week()
    {
        var userId = Guid.NewGuid();
        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();
        var monday = ThisMondayInstant;

        // Two completed sessions in gym A this week, one in gym B → gym A's assignment is authoritative.
        var sessions = new[]
        {
            InTenant(CompletedSession(userId, monday), gymA),
            InTenant(CompletedSession(userId, monday.AddDays(1)), gymA),
            InTenant(CompletedSession(userId, monday.AddDays(2)), gymB),
        };

        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gymA, 4, ThisMonday.AddDays(-7 * 20)),
            new(Guid.NewGuid(), gymB, 2, ThisMonday.AddDays(-7 * 16)),
        };

        var result = await Run(userId, sessions, assignments);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ThisWeek.HasActivePlan);
        Assert.Equal(4, result.Value!.ThisWeek.Goal);   // gym A frequency, not gym B
    }

    [Fact]
    public async Task Goal_tie_break_prefers_the_latest_start_date()
    {
        var userId = Guid.NewGuid();
        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();

        // No completed sessions this week → both gyms tie at 0; latest StartDate wins.
        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gymA, 3, ThisMonday.AddDays(-7 * 20)),
            new(Guid.NewGuid(), gymB, 5, ThisMonday.AddDays(-7 * 4)),   // later start
        };

        var result = await Run(userId, [], assignments);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.ThisWeek.Goal);
    }

    [Fact]
    public async Task Ad_hoc_user_without_an_active_assignment_has_null_goal_and_no_plan()
    {
        var userId = Guid.NewGuid();
        var monday = ThisMondayInstant;

        var result = await Run(userId,
            [CompletedSession(userId, monday)],
            activeAssignments: []);   // no active assignment

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ThisWeek.Goal);
        Assert.False(result.Value!.ThisWeek.HasActivePlan);
        Assert.Equal(1, result.Value!.ThisWeek.CompletedSessions);
        // No goal ⇒ consistency % and streak are not exposed.
        Assert.Null(result.Value!.Consistency.ConsistencyPct);
        Assert.Equal(0, result.Value!.Consistency.CurrentStreakWeeks);
    }

    [Fact]
    public async Task Ad_hoc_sessions_count_toward_the_weekly_total_even_when_a_plan_is_active()
    {
        // Symmetric to nutrition D15: self-training (Source = Adhoc, no PlanAssignment) is real work
        // and must show on Progress. The weekly total is completed-by-window and is NEVER filtered to
        // plan-sourced sessions — a future `Source == FromAssignment` filter on the count breaks this.
        var userId = Guid.NewGuid();
        var gym = Guid.NewGuid();
        var monday = ThisMondayInstant;

        // An active plan (so a goal exists) plus three ad-hoc, no-plan completed sessions this week.
        var sessions = new[]
        {
            InTenant(CompletedSession(userId, monday), gym),
            InTenant(CompletedSession(userId, monday.AddDays(1)), gym),
            InTenant(CompletedSession(userId, monday.AddDays(2)), gym),
        };
        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gym, 4, ThisMonday.AddDays(-7 * 8)),
        };

        var result = await Run(userId, sessions, assignments);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ThisWeek.HasActivePlan);
        Assert.Equal(4, result.Value!.ThisWeek.Goal);
        // The three ad-hoc sessions are all counted — the weekly total never filters them out.
        Assert.Equal(3, result.Value!.ThisWeek.CompletedSessions);
    }

    // ── consistency % (D10) ──

    [Fact]
    public async Task Consistency_pct_observes_weeks_from_the_first_session_not_a_flat_twelve()
    {
        var userId = Guid.NewGuid();
        var gym = Guid.NewGuid();

        // D10: the first completed session is 2 weeks ago (so the 10 empty weeks BEFORE it are NOT counted),
        // and the goal of 2/week is hit in BOTH observed weeks → 100%, with a 2-week streak. A flat-12
        // denominator would dilute this to 2/12 ≈ 17%; D10 forgives the newcomer.
        var sessions = new List<WorkoutSession>
        {
            // Week −1 (first session in the window): two completed sessions ⇒ goal hit.
            InTenant(CompletedSession(userId, MondayInstant(1)), gym),
            InTenant(CompletedSession(userId, MondayInstant(1).AddDays(2)), gym),
            // Week 0 (this week): two completed sessions ⇒ goal hit.
            InTenant(CompletedSession(userId, MondayInstant(0)), gym),
            InTenant(CompletedSession(userId, MondayInstant(0).AddDays(2)), gym),
        };

        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gym, 2, ThisMonday.AddDays(-7 * 4)),   // goal = 2 sessions/week
        };

        var result = await Run(userId, sessions, assignments);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ThisWeek.Goal);
        // 2 weeks observed (first session through this week), both hit the goal → 100, not 17.
        Assert.Equal(100, result.Value!.Consistency.ConsistencyPct);
        Assert.Equal(2, result.Value!.Consistency.CurrentStreakWeeks);
    }

    [Fact]
    public async Task Consistency_pct_is_null_when_there_is_a_goal_but_no_sessions()
    {
        var userId = Guid.NewGuid();
        var gym = Guid.NewGuid();

        // A goal exists, but the user has logged nothing in the window → no observed weeks ⇒ null (D10),
        // never 0% (which would falsely brand a brand-new member a total laggard).
        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gym, 3, ThisMonday.AddDays(-7 * 4)),
        };

        var result = await Run(userId, [], assignments);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.ThisWeek.Goal);
        Assert.Null(result.Value!.Consistency.ConsistencyPct);
        Assert.Equal(0, result.Value!.Consistency.CurrentStreakWeeks);
    }

    // ── honesty gate ──

    [Fact]
    public async Task Honesty_gate_excludes_high_rep_non_working_and_non_strength_sets()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();
        var treadmill = Guid.NewGuid();

        // 4 sessions so the lift clears the ≥4 bar — each contributes exactly one qualifying point (the
        // 100kg×5 working set). The disqualified rows must never raise the e1RM.
        var sessions = Enumerable.Range(0, 4)
            .Select(i => CompletedSession(userId, MondayInstant(i),
                Exercise(bench, "Bench", ExerciseTrackingType.Strength,
                    WorkingSet(5, 100m),                          // qualifies → e1RM 116.7
                    Set(PerformedSetType.Working, 20, 200m),      // excluded: reps > 12 (huge e1RM if counted)
                    Set(PerformedSetType.Warmup, 3, 300m)),       // excluded: not a working set
                Exercise(treadmill, "Treadmill", ExerciseTrackingType.Cardio,
                    Set(PerformedSetType.Working, 1, 999m))))     // excluded: cardio tracking type
            .ToList();

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        var lift = Assert.Single(result.Value!.TopLifts);
        Assert.Equal(bench, lift.ExerciseId);
        // 100 × (1 + 5/30) = 116.7 — proves reps>12 / warmup / cardio rows were all gated out.
        Assert.Equal(116.7m, lift.CurrentE1rmKg);
        Assert.DoesNotContain(treadmill, result.Value!.TopLifts.Select(l => l.ExerciseId));
    }

    [Fact]
    public async Task One_point_per_session_is_the_max_e1rm_and_drop_clusters_are_not_excluded()
    {
        var userId = Guid.NewGuid();
        var squat = Guid.NewGuid();

        // Each session has a lead working set + a heavier working set + a drop stage (ParentSetId set).
        // The session point must be the MAX qualifying working-set e1RM, and the drop stage (null e1RM,
        // since Drop type ⇒ no e1RM) must neither be counted nor cause the lift to be dropped.
        var sessions = Enumerable.Range(0, 4)
            .Select(i =>
            {
                var lead = WorkingSet(5, 100m);                                    // 116.7
                var heavier = WorkingSet(3, 120m);                                 // 132.0 ← MAX
                var dropStage = Set(PerformedSetType.Drop, 8, 60m, parent: lead.Id); // null e1RM
                return CompletedSession(userId,
                    MondayInstant(i),
                    Exercise(squat, "Squat", ExerciseTrackingType.Strength, lead, heavier, dropStage));
            })
            .ToList();

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        var lift = Assert.Single(result.Value!.TopLifts);
        Assert.Equal(132.0m, lift.CurrentE1rmKg);
        // 4 sessions → 4 spark points (one per session), not per set.
        Assert.Equal(4, lift.SparkE1rmKg.Count);
        Assert.All(lift.SparkE1rmKg, p => Assert.Equal(132.0m, p));
    }

    // ── top-lift selection ──

    [Fact]
    public async Task Lift_with_fewer_than_four_sessions_is_omitted()
    {
        var userId = Guid.NewGuid();
        var rare = Guid.NewGuid();

        var sessions = Enumerable.Range(0, 3)   // only 3 exposures
            .Select(i => CompletedSession(userId,
                MondayInstant(i),
                Exercise(rare, "Rare Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))))
            .ToList();

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.TopLifts);
    }

    [Fact]
    public async Task Top_lifts_are_the_three_most_frequent_qualifying_lifts()
    {
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        // a:7, b:6, c:5, d:4 qualifying sessions. Top-3 by count = a, b, c (d is squeezed out).
        var sessions = new List<WorkoutSession>();
        void Add(Guid lift, int count)
        {
            for (var i = 0; i < count; i++)
                sessions.Add(CompletedSession(userId, MondayInstant(i),
                    Exercise(lift, lift.ToString(), ExerciseTrackingType.Strength, WorkingSet(5, 100m))));
        }
        Add(a, 7); Add(b, 6); Add(c, 5); Add(d, 4);

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        var ids = result.Value!.TopLifts.Select(l => l.ExerciseId).ToList();
        Assert.Equal(3, ids.Count);
        Assert.Equal(new[] { a, b, c }, ids);   // ordered by session-count desc
        Assert.DoesNotContain(d, ids);
    }

    // ── direction + stall ──

    [Fact]
    public async Task Direction_is_up_when_current_exceeds_the_trailing_baseline()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Older 4 weeks ~100kg working sets, newest week a clear jump → Up.
        var sessions = new List<WorkoutSession>
        {
            CompletedSession(userId, MondayInstant(4), Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(3), Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(2), Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(1), Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(0), Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 120m))),
        };

        var result = await Run(userId, sessions);

        var dto = Assert.Single(result.Value!.TopLifts);
        Assert.Equal(LiftTrendDirection.Up, dto.Direction);
        Assert.True(dto.DeltaKgVsTrailing4w > 0.5m);
    }

    [Fact]
    public async Task Stall_is_flagged_when_no_new_best_in_the_last_three_exposures()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Best set early, then three flat-or-lower exposures → stalled, 3 exposures since the best.
        var weights = new (int weeksAgo, decimal weight)[]
        {
            (4, 120m),   // best (e1RM 140.0)
            (3, 110m),
            (2, 110m),
            (1, 110m),
        };
        var sessions = weights
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();

        var result = await Run(userId, sessions);

        var dto = Assert.Single(result.Value!.TopLifts);
        Assert.True(dto.Stalled);
        Assert.Equal(3, dto.StallSessions);
    }

    [Fact]
    public async Task A_fresh_best_on_the_latest_exposure_is_not_stalled()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        var weights = new (int weeksAgo, decimal weight)[]
        {
            (3, 100m),
            (2, 105m),
            (1, 110m),
            (0, 120m),   // newest is a new best
        };
        var sessions = weights
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();

        var result = await Run(userId, sessions);

        var dto = Assert.Single(result.Value!.TopLifts);
        Assert.False(dto.Stalled);
        Assert.Equal(0, dto.StallSessions);
    }

    // ── user-selectable window (?weeks=) ──

    [Fact]
    public async Task Weeks_null_defaults_to_a_twelve_week_window()
    {
        var userId = Guid.NewGuid();

        // A session 8 weeks ago is INSIDE the default 12-week window → it shows in the consistency days,
        // and the reported WindowWeeks is the default 12.
        var result = await Run(userId,
            [CompletedSession(userId, MondayInstant(8))],
            weeks: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.Consistency.WindowWeeks);
        Assert.Single(result.Value!.Consistency.Days);
    }

    [Fact]
    public async Task Weeks_four_narrows_the_consistency_window()
    {
        var userId = Guid.NewGuid();

        // Two sessions: one this week (always in any window), one 6 weeks ago. A 4-week window covers
        // weeks 0..3, so the 6-weeks-ago session falls OUTSIDE it and must be dropped from consistency.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0)),
            CompletedSession(userId, MondayInstant(6)),
        };

        // Default 12-week window: both sessions are in range → 2 consistency days.
        var wide = await Run(userId, sessions, weeks: null);
        Assert.True(wide.IsSuccess);
        Assert.Equal(12, wide.Value!.Consistency.WindowWeeks);
        Assert.Equal(2, wide.Value!.Consistency.Days.Count);

        // weeks=4: the 6-weeks-ago session is outside the window → only the current-week day remains.
        var narrow = await Run(userId, sessions, weeks: 4);
        Assert.True(narrow.IsSuccess);
        Assert.Equal(4, narrow.Value!.Consistency.WindowWeeks);
        Assert.Single(narrow.Value!.Consistency.Days);
        Assert.Equal(ThisMonday, narrow.Value!.Consistency.Days[0].Date);
    }

    [Fact]
    public async Task Weeks_one_shows_only_the_current_week()
    {
        var userId = Guid.NewGuid();

        // weeks=1 is the narrowest selectable window — the current Monday-week only (the "Week" tab). A
        // session ONE week ago falls outside it and is dropped; only the current-week day remains.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0)),
            CompletedSession(userId, MondayInstant(1)),
        };

        var result = await Run(userId, sessions, weeks: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Consistency.WindowWeeks);
        Assert.Single(result.Value!.Consistency.Days);            // last-week session excluded
        Assert.Equal(ThisMonday, result.Value!.Consistency.Days[0].Date);
    }

    [Fact]
    public async Task Weeks_below_minimum_clamps_up_to_one()
    {
        var userId = Guid.NewGuid();

        // weeks=0 is below the floor → clamps to 1, and the reported WindowWeeks reflects the EFFECTIVE
        // clamped window. A session one week ago is outside the clamped current-week window → dropped.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0)),
            CompletedSession(userId, MondayInstant(1)),
        };

        var result = await Run(userId, sessions, weeks: 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Consistency.WindowWeeks);   // clamped 0 → 1
        Assert.Single(result.Value!.Consistency.Days);            // one-week-ago session excluded
    }

    [Fact]
    public async Task Weeks_above_maximum_clamps_down_to_fifty_two()
    {
        var userId = Guid.NewGuid();

        // weeks=99 is above the ceiling → clamps to 52. A session 40 weeks ago is inside the clamped
        // 52-week window → it shows, and WindowWeeks reports the EFFECTIVE 52.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0)),
            CompletedSession(userId, MondayInstant(40)),
        };

        var result = await Run(userId, sessions, weeks: 99);

        Assert.True(result.IsSuccess);
        Assert.Equal(52, result.Value!.Consistency.WindowWeeks);  // clamped 99 → 52
        Assert.Equal(2, result.Value!.Consistency.Days.Count);    // both in the 52-week window
    }

    [Fact]
    public async Task This_week_hero_and_goal_are_unaffected_by_the_selected_window()
    {
        var userId = Guid.NewGuid();
        var gym = Guid.NewGuid();

        // Two completed sessions this week plus one 6 weeks ago; an active 4/week plan supplies the goal.
        // The window only governs consistency/heatmap/top-lifts — the hero (current-week count + WeekStart)
        // and the D1 goal must be identical whether the window is narrow (4) or default (12).
        var sessions = new[]
        {
            InTenant(CompletedSession(userId, MondayInstant(0)), gym),
            InTenant(CompletedSession(userId, MondayInstant(0).AddDays(2)), gym),
            InTenant(CompletedSession(userId, MondayInstant(6)), gym),
        };
        var assignments = new List<OwnActiveAssignmentDto>
        {
            new(Guid.NewGuid(), gym, 4, ThisMonday.AddDays(-7 * 8)),
        };

        var narrow = await Run(userId, sessions, assignments, weeks: 4);
        var wide = await Run(userId, sessions, assignments, weeks: 12);

        Assert.True(narrow.IsSuccess);
        Assert.True(wide.IsSuccess);

        // Hero week + current-week completed count + goal are identical across windows.
        Assert.Equal(ThisMonday, narrow.Value!.ThisWeek.WeekStart);
        Assert.Equal(wide.Value!.ThisWeek.WeekStart, narrow.Value!.ThisWeek.WeekStart);
        Assert.Equal(2, narrow.Value!.ThisWeek.CompletedSessions);
        Assert.Equal(wide.Value!.ThisWeek.CompletedSessions, narrow.Value!.ThisWeek.CompletedSessions);
        Assert.Equal(4, narrow.Value!.ThisWeek.Goal);
        Assert.Equal(wide.Value!.ThisWeek.Goal, narrow.Value!.ThisWeek.Goal);

        // But the consistency window itself DID change with the selection.
        Assert.Equal(4, narrow.Value!.Consistency.WindowWeeks);
        Assert.Equal(12, wide.Value!.Consistency.WindowWeeks);
    }

    // ── PR teaser ──

    [Fact]
    public async Task Pr_teaser_takes_the_top_three_records_and_never_a_count()
    {
        var userId = Guid.NewGuid();
        var records = new List<PersonalRecordDto>
        {
            new(Guid.NewGuid(), "Deadlift", 180m, 3, 198m, NowUtc),
            new(Guid.NewGuid(), "Squat", 150m, 5, 175m, NowUtc),
            new(Guid.NewGuid(), "Bench", 120m, 5, 140m, NowUtc),
            new(Guid.NewGuid(), "OHP", 70m, 5, 81m, NowUtc),
        };

        var result = await Run(userId, [], records: records);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.RecentPrs.Count);
        Assert.Equal(new[] { "Deadlift", "Squat", "Bench" },
            result.Value!.RecentPrs.Select(p => p.ExerciseName).ToArray());
    }

    [Fact]
    public async Task Pr_teaser_keeps_only_records_set_within_the_window()
    {
        var userId = Guid.NewGuid();
        // Deadlift PR set 2 weeks ago (inside a 4-week window); Squat PR set 10 weeks ago (outside it).
        var records = new List<PersonalRecordDto>
        {
            new(Guid.NewGuid(), "Deadlift", 180m, 3, 198m, MondayInstant(2)),
            new(Guid.NewGuid(), "Squat", 150m, 5, 175m, MondayInstant(10)),
        };

        var result = await Run(userId, [], records: records, weeks: 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Deadlift" },
            result.Value!.RecentPrs.Select(p => p.ExerciseName).ToArray());
    }

    // ── v2 window differentiation (WINDOW-DIFFERENTIATION.md) ──

    [Fact]
    public async Task Period_stats_split_the_current_window_from_the_previous_equal_length_window()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();
        PerformedExercise Bench() =>
            Exercise(lift, "Bench", ExerciseTrackingType.Strength, WorkingSet(5, 100m)); // 500kg vol, 1 hard set

        // weeks=4 → current window = weeks 0..3, previous window = weeks 4..7.
        var result = await Run(userId,
        [
            CompletedSession(userId, MondayInstant(0), Bench()),  // current
            CompletedSession(userId, MondayInstant(1), Bench()),  // current
            CompletedSession(userId, MondayInstant(4), Bench()),  // previous window
        ], weeks: 4);

        Assert.True(result.IsSuccess);
        var p = result.Value!.Period;
        Assert.Equal(2, p.Sessions);
        Assert.Equal(1, p.PrevSessions);
        Assert.Equal(1000m, p.VolumeKg);
        Assert.Equal(500m, p.PrevVolumeKg);
        Assert.Equal(2, p.WorkingSets);
        Assert.Equal(1, p.PrevWorkingSets);
        Assert.Equal(4, p.WeeklyVolumeKg.Count);       // one entry per window week
        Assert.Equal(500m, p.WeeklyVolumeKg[^1]);      // newest entry = this week
        Assert.Equal(1000m, p.WeeklyVolumeKg.Sum());
    }

    [Fact]
    public async Task Strength_gain_is_first_to_latest_e1rm_over_the_window()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Four ascending sessions: 100→105→110→120 kg × 5 → e1RM 116.7 → 140.0.
        var weights = new (int weeksAgo, decimal weight)[] { (3, 100m), (2, 105m), (1, 110m), (0, 120m) };
        var sessions = weights
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Squat", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        var gain = Assert.Single(result.Value!.StrengthGain.Lifts);
        Assert.Equal(116.7m, gain.StartE1rmKg);
        Assert.Equal(140.0m, gain.CurrentE1rmKg);
        Assert.Equal(23.3m, gain.GainKg);
        Assert.Equal(0, gain.PlateauWeeks);            // newest session is the best → still climbing
        Assert.Equal(20.0m, result.Value!.StrengthGain.AvgGainPct);
    }

    [Fact]
    public async Task Muscle_volume_averages_working_lead_sets_per_week_by_primary_group()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();
        PerformedExercise Bench() =>
            Exercise(bench, "Bench", ExerciseTrackingType.Strength,
                WorkingSet(5, 100m), WorkingSet(5, 100m), WorkingSet(5, 100m)); // 3 hard sets

        // weeks=4 → ÷4. Current: 2×3 = 6 sets → 1.5/wk. Previous: 1×3 = 3 → 0.75 → 0.8/wk at 1 dp (parity with
        // how volume/e1RM are rounded server-side).
        var result = await Run(userId,
        [
            CompletedSession(userId, MondayInstant(0), Bench()),
            CompletedSession(userId, MondayInstant(1), Bench()),
            CompletedSession(userId, MondayInstant(4), Bench()),
        ],
        weeks: 4,
        muscleMap: new Dictionary<Guid, string> { [bench] = "chest" });

        Assert.True(result.IsSuccess);
        var m = Assert.Single(result.Value!.MuscleVolume);
        Assert.Equal("chest", m.Muscle);
        Assert.Equal(1.5m, m.SetsPerWeek);
        Assert.Equal(0.8m, m.PrevSetsPerWeek);
    }

    [Fact]
    public async Task Load_balance_flags_ramping_when_the_recent_week_dwarfs_the_chronic_average()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // One 500kg session this week, nothing prior → chronic 28d Σ = 500, weekly avg = 125; acute (7d) = 500 >
        // 125 × 1.5 → Ramping. Two raw volumes are exposed, never a ratio (R10).
        var result = await Run(userId,
            [CompletedSession(userId, ThisMondayInstant,
                Exercise(lift, "Bench", ExerciseTrackingType.Strength, WorkingSet(5, 100m)))],
            weeks: 4);

        Assert.True(result.IsSuccess);
        var load = result.Value!.Load;
        Assert.Equal(500m, load.AcuteVolumeKg);
        Assert.Equal(125m, load.ChronicWeeklyVolumeKg);
        Assert.Equal(LoadTrend.Ramping, load.Trend);
    }

    [Fact]
    public async Task Coach_read_is_an_honest_empty_state_for_a_brand_new_user()
    {
        var userId = Guid.NewGuid();

        var result = await Run(userId, []);

        Assert.True(result.IsSuccess);
        var coach = result.Value!.Coach;
        Assert.Equal(CoachTone.Neutral, coach.Tone);
        Assert.Null(coach.Action);
        Assert.Contains("not enough", coach.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coach_read_surfaces_a_stalled_lift_as_the_block_action()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Best early, then three flat exposures → the lift is stalled; the 4-week (block) coach action calls it out.
        var weights = new (int weeksAgo, decimal weight)[] { (3, 120m), (2, 110m), (1, 110m), (0, 110m) };
        var sessions = weights
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Squat", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();

        var result = await Run(userId, sessions, weeks: 4);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Coach.Action);
        Assert.Contains("stall", result.Value!.Coach.Action!, StringComparison.OrdinalIgnoreCase);
    }

    private Task<Result<ProgressOverviewDto>> WithSessions(Guid userId, params WorkoutSession[] sessions)
        => Run(userId, sessions);

    private static WorkoutSession InTenant(WorkoutSession session, Guid tenantId)
    {
        SetProp(session, "TenantId", (Guid?)tenantId);
        return session;
    }
}
