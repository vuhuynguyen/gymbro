using System.Reflection;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The full strength-lift list math (api/me/exercises/strength-lifts, Phase 2), fully mocked — no database.
/// Pins the frozen rules: the SAME windowed e1RM gathering as the overview (shared StrengthLiftSeries +
/// E1rmSeriesCalculator) but UNCAPPED (all performed lifts, never top-3); the honesty gate applied as a FLAG
/// (HasTrend ⇔ ≥ 4 qualifying sessions) with NO fabricated direction below the bar; primary-muscle enrichment
/// via the mocked ResolveExerciseMuscleGroupsQuery; the optional primary-group filter; e1RM-desc sort; and
/// self-scoping (QueryOwnAcrossGyms only ever asked for the caller's own id). Time-relative to UtcNow (no
/// calendar time-bomb).
/// </summary>
public sealed class GetMyStrengthLiftsHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static readonly DateOnly ThisMonday = MondayOfUtcWeek(DateTimeOffset.UtcNow);

    private static readonly DateTimeOffset ThisMondayInstant =
        new(ThisMonday.Year, ThisMonday.Month, ThisMonday.Day, 9, 0, 0, TimeSpan.Zero);

    private static DateOnly MondayOfUtcWeek(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    private static DateTimeOffset MondayInstant(int weeksAgo) => ThisMondayInstant.AddDays(-7 * weeksAgo);

    // ── handler wiring ──

    private static (GetMyStrengthLiftsHandler Sut, IWorkoutSessionRepository Repo) CreateSut(
        Guid userId,
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyDictionary<Guid, string>? muscleMap = null)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.QueryOwnAcrossGyms(userId)
            .Returns(new TestAsyncEnumerable<WorkoutSession>(sessions));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveExerciseMuscleGroupsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, string>>.Success(
                muscleMap ?? new Dictionary<Guid, string>()));

        return (new GetMyStrengthLiftsHandler(repo, mediator, currentUser), repo);
    }

    private static Task<Result<StrengthLiftListDto>> Run(
        Guid userId,
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyDictionary<Guid, string>? muscleMap = null,
        int? weeks = null,
        string? muscleGroup = null)
    {
        var (sut, _) = CreateSut(userId, sessions, muscleMap);
        return sut.Handle(new GetMyStrengthLiftsQuery(weeks, muscleGroup), CancellationToken.None);
    }

    // ── entity seeding (private setters + UtcNow stamps ⇒ reflection) ──

    private static WorkoutSession CompletedSession(
        Guid traineeId, DateTimeOffset startedAt, params PerformedExercise[] exercises)
    {
        var session = WorkoutSession.Start(
            traineeId, Tenant, SessionSource.Adhoc, null, null, "Lift", null, "UTC", null);
        SetProp(session, "StartedAt", startedAt);
        SetProp(session, "Status", SessionStatus.Completed);
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

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    private static List<T> Backing<T>(object target, string field)
        => (List<T>)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(target)!;

    // Build N weekly sessions for one lift, oldest first, each a single working set at the given weight.
    private static IEnumerable<WorkoutSession> WeeklyLift(
        Guid userId, Guid lift, string name, int sessions, decimal weightKg)
        => Enumerable.Range(0, sessions)
            .Select(i => CompletedSession(userId, MondayInstant(sessions - 1 - i),
                Exercise(lift, name, ExerciseTrackingType.Strength, WorkingSet(5, weightKg))));

    // ── new user ──

    [Fact]
    public async Task New_user_with_no_sessions_returns_200_with_empty_list()
    {
        var result = await Run(Guid.NewGuid(), []);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Lifts);
    }

    // ── UNCAPPED: all performed lifts, never top-3 ──

    [Fact]
    public async Task Returns_all_performed_strength_lifts_not_capped_at_three()
    {
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        var e = Guid.NewGuid();

        // Five distinct lifts, each with 4 qualifying sessions — the overview would cap at 3; this list must
        // return all FIVE. Distinct weights so the e1RM-desc sort is observable.
        var sessions = new List<WorkoutSession>();
        sessions.AddRange(WeeklyLift(userId, a, "A", 4, 140m));
        sessions.AddRange(WeeklyLift(userId, b, "B", 4, 120m));
        sessions.AddRange(WeeklyLift(userId, c, "C", 4, 100m));
        sessions.AddRange(WeeklyLift(userId, d, "D", 4, 80m));
        sessions.AddRange(WeeklyLift(userId, e, "E", 4, 60m));

        var result = await Run(userId, sessions);

        Assert.True(result.IsSuccess);
        var lifts = result.Value!.Lifts;
        Assert.Equal(5, lifts.Count);
        // Sorted by current e1RM desc (heavier lift first).
        Assert.Equal(new[] { a, b, c, d, e }, lifts.Select(l => l.ExerciseId).ToArray());
        Assert.True(lifts[0].CurrentE1rmKg > lifts[1].CurrentE1rmKg);
    }

    // ── honesty gate as a FLAG (no fabricated direction) ──

    [Fact]
    public async Task Thin_lift_under_four_sessions_has_no_trend_and_no_fabricated_direction()
    {
        var userId = Guid.NewGuid();
        var thin = Guid.NewGuid();

        // Only 3 qualifying sessions, each a clear increase — enough to LOOK like an Up trend, but below the
        // ≥4 honesty bar. The lift must still appear (uncapped) but HasTrend=false and all trend fields default.
        var sessions = new List<WorkoutSession>
        {
            CompletedSession(userId, MondayInstant(2),
                Exercise(thin, "Thin", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(1),
                Exercise(thin, "Thin", ExerciseTrackingType.Strength, WorkingSet(5, 110m))),
            CompletedSession(userId, MondayInstant(0),
                Exercise(thin, "Thin", ExerciseTrackingType.Strength, WorkingSet(5, 130m))),
        };

        var result = await Run(userId, sessions);

        var lift = Assert.Single(result.Value!.Lifts);
        Assert.Equal(thin, lift.ExerciseId);
        Assert.Equal(3, lift.SessionCount);
        Assert.True(lift.CurrentE1rmKg > 0);          // e1RM is still reported
        Assert.False(lift.HasTrend);                   // below the ≥4 honesty bar
        Assert.Equal(LiftTrendDirection.Flat, lift.Direction);  // NEVER fabricated (default Flat)
        Assert.False(lift.Stalled);
        Assert.Equal(0, lift.StallSessions);
        Assert.Empty(lift.SparkE1rmKg);
    }

    [Fact]
    public async Task Lift_with_four_or_more_sessions_has_a_real_trend()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // 4 flat weeks at 100kg then a clear jump → ≥4 sessions, so HasTrend=true with a real Up direction.
        var sessions = new List<WorkoutSession>
        {
            CompletedSession(userId, MondayInstant(4), Exercise(lift, "L", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(3), Exercise(lift, "L", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(2), Exercise(lift, "L", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(1), Exercise(lift, "L", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
            CompletedSession(userId, MondayInstant(0), Exercise(lift, "L", ExerciseTrackingType.Strength, WorkingSet(5, 120m))),
        };

        var result = await Run(userId, sessions);

        var dto = Assert.Single(result.Value!.Lifts);
        Assert.Equal(5, dto.SessionCount);
        Assert.True(dto.HasTrend);
        Assert.Equal(LiftTrendDirection.Up, dto.Direction);
        Assert.NotEmpty(dto.SparkE1rmKg);
    }

    // ── primary-muscle enrichment + filter ──

    [Fact]
    public async Task Enriches_each_lift_with_its_primary_muscle_group()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();
        var squat = Guid.NewGuid();

        var sessions = new List<WorkoutSession>();
        sessions.AddRange(WeeklyLift(userId, bench, "Bench", 4, 120m));
        sessions.AddRange(WeeklyLift(userId, squat, "Squat", 4, 160m));

        var muscleMap = new Dictionary<Guid, string>
        {
            [bench] = "chest",
            [squat] = "legs",
        };

        var result = await Run(userId, sessions, muscleMap);

        var lifts = result.Value!.Lifts;
        Assert.Equal("legs", lifts.Single(l => l.ExerciseId == squat).PrimaryMuscleGroup);
        Assert.Equal("chest", lifts.Single(l => l.ExerciseId == bench).PrimaryMuscleGroup);
    }

    [Fact]
    public async Task Unresolved_muscle_group_is_null()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();
        var sessions = WeeklyLift(userId, lift, "L", 4, 100m);

        // Empty muscle map → the lift's primary group is null (never fabricated).
        var result = await Run(userId, sessions, muscleMap: new Dictionary<Guid, string>());

        var dto = Assert.Single(result.Value!.Lifts);
        Assert.Null(dto.PrimaryMuscleGroup);
    }

    [Fact]
    public async Task Muscle_group_filter_narrows_to_matching_primary_group()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();   // chest
        var squat = Guid.NewGuid();   // legs
        var press = Guid.NewGuid();   // shoulders

        var sessions = new List<WorkoutSession>();
        sessions.AddRange(WeeklyLift(userId, bench, "Bench", 4, 120m));
        sessions.AddRange(WeeklyLift(userId, squat, "Squat", 4, 160m));
        sessions.AddRange(WeeklyLift(userId, press, "Press", 4, 80m));

        var muscleMap = new Dictionary<Guid, string>
        {
            [bench] = "chest",
            [squat] = "legs",
            [press] = "shoulders",
        };

        var result = await Run(userId, sessions, muscleMap, muscleGroup: "legs");

        var dto = Assert.Single(result.Value!.Lifts);
        Assert.Equal(squat, dto.ExerciseId);
        Assert.Equal("legs", dto.PrimaryMuscleGroup);
    }

    [Fact]
    public async Task Null_muscle_group_filter_returns_every_lift()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();
        var squat = Guid.NewGuid();

        var sessions = new List<WorkoutSession>();
        sessions.AddRange(WeeklyLift(userId, bench, "Bench", 4, 120m));
        sessions.AddRange(WeeklyLift(userId, squat, "Squat", 4, 160m));

        var muscleMap = new Dictionary<Guid, string> { [bench] = "chest", [squat] = "legs" };

        var result = await Run(userId, sessions, muscleMap, muscleGroup: null);

        Assert.Equal(2, result.Value!.Lifts.Count);
    }

    // ── self-scope / IDOR ──

    [Fact]
    public async Task Read_only_queries_the_callers_own_sessions()
    {
        var userA = Guid.NewGuid();
        var lift = Guid.NewGuid();

        var (sut, repo) = CreateSut(userA, WeeklyLift(userA, lift, "L", 4, 100m));
        var result = await sut.Handle(new GetMyStrengthLiftsQuery(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The repository is only ever asked for the caller's own id — another user's sessions are unreachable.
        repo.Received(1).QueryOwnAcrossGyms(userA);
        repo.DidNotReceive().QueryOwnAcrossGyms(Arg.Is<Guid>(id => id != userA));
    }
}
