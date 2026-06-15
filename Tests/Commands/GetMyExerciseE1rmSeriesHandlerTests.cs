using System.Reflection;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using Modules.WorkoutSessionModule.Application;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The per-lift e1RM drill-down math (api/me/exercises/{id}/e1rm-series, Phase 2), fully mocked — no database.
/// Pins the frozen rules from API-CONTRACTS §2: the SAME honesty gate as the overview (Working + e1RM +
/// reps ≤ 12 + Strength/Bodyweight); one MAX point per session capturing the top set's weight/reps; IsPr =
/// strictly exceeds the running max; 200 + empty Points for an unknown/never-trained lift; self-scoping
/// (QueryOwnAcrossGyms is only ever asked for the caller's own id); and parity with the overview's strength
/// summary via the shared <see cref="E1rmSeriesCalculator"/>. Time-relative to UtcNow (no calendar time-bomb).
/// </summary>
public sealed class GetMyExerciseE1rmSeriesHandlerTests
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

    private static DateTimeOffset MondayInstant(int weeksAgo)
        => ThisMondayInstant.AddDays(-7 * weeksAgo);

    // ── handler wiring ──

    private static GetMyExerciseE1rmSeriesHandler CreateSut(Guid userId, IEnumerable<WorkoutSession> sessions)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.QueryOwnAcrossGyms(userId)
            .Returns(new TestAsyncEnumerable<WorkoutSession>(sessions));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        return new GetMyExerciseE1rmSeriesHandler(repo, currentUser);
    }

    private static Task<Result<ExerciseE1rmSeriesDto>> Run(
        Guid userId, Guid exerciseId, IEnumerable<WorkoutSession> sessions,
        DateOnly? from = null, DateOnly? to = null)
        => CreateSut(userId, sessions)
            .Handle(new GetMyExerciseE1rmSeriesQuery(exerciseId, from, to), CancellationToken.None);

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

    // ── unknown / empty lift ──

    [Fact]
    public async Task Unknown_lift_returns_200_with_empty_points()
    {
        var userId = Guid.NewGuid();
        var unknown = Guid.NewGuid();

        // The caller trained a DIFFERENT lift; the requested id was never trained.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0),
                Exercise(Guid.NewGuid(), "Other", ExerciseTrackingType.Strength, WorkingSet(5, 100m)))
        };

        var result = await Run(userId, unknown, sessions);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(unknown, dto.ExerciseId);
        Assert.Empty(dto.Points);
        Assert.Equal(0m, dto.CurrentE1rmKg);
        Assert.Equal(LiftTrendDirection.Flat, dto.Direction);
        Assert.False(dto.Stalled);
        // Unknown lift ⇒ no name and the default Strength tag (never throws / 404).
        Assert.Null(dto.ExerciseName);
        Assert.Equal("Strength", dto.TrackingType);
    }

    [Fact]
    public async Task New_user_with_no_sessions_returns_empty_points()
    {
        var result = await Run(Guid.NewGuid(), Guid.NewGuid(), []);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Points);
    }

    // ── honesty gate ──

    [Fact]
    public async Task Honesty_gate_excludes_high_rep_non_working_and_non_strength_rows()
    {
        var userId = Guid.NewGuid();
        var bench = Guid.NewGuid();

        // One session: only the 100×5 working set qualifies (e1RM 116.7). Reps>12, warmup, and a cardio-tagged
        // exercise must never raise the session best.
        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0),
                Exercise(bench, "Bench", ExerciseTrackingType.Strength,
                    WorkingSet(5, 100m),                       // qualifies → 116.7
                    Set(PerformedSetType.Working, 20, 200m),   // excluded: reps > 12
                    Set(PerformedSetType.Warmup, 3, 300m))),   // excluded: warmup
            // Same lift id but tracked as cardio in a different session → excluded entirely.
            CompletedSession(userId, MondayInstant(1),
                Exercise(bench, "Bench", ExerciseTrackingType.Cardio,
                    Set(PerformedSetType.Working, 1, 999m)))
        };

        var result = await Run(userId, bench, sessions);

        Assert.True(result.IsSuccess);
        var point = Assert.Single(result.Value!.Points);
        Assert.Equal(116.7m, point.SessionBestE1rmKg);
        Assert.Equal(100m, point.TopSetWeightKg);
        Assert.Equal(5, point.TopSetReps);
    }

    // ── MAX-per-session + drop cluster + top-set capture ──

    [Fact]
    public async Task One_point_per_session_is_the_max_and_captures_the_top_set_weight_and_reps()
    {
        var userId = Guid.NewGuid();
        var squat = Guid.NewGuid();

        // Lead 100×5 (116.7) + heavier 120×3 (132.0 = MAX) + a Drop stage (null e1RM). The point must be the
        // MAX, and its TopSetWeight/Reps must come from the heavier set, not the lead — and the drop cluster
        // must neither be counted nor exclude the session.
        var lead = WorkingSet(5, 100m);
        var heavier = WorkingSet(3, 120m);
        var drop = Set(PerformedSetType.Drop, 8, 60m, parent: lead.Id);

        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(0),
                Exercise(squat, "Squat", ExerciseTrackingType.Strength, lead, heavier, drop))
        };

        var result = await Run(userId, squat, sessions);

        var point = Assert.Single(result.Value!.Points);
        Assert.Equal(132.0m, point.SessionBestE1rmKg);
        Assert.Equal(120m, point.TopSetWeightKg);
        Assert.Equal(3, point.TopSetReps);
    }

    // ── IsPr (running max) ──

    [Fact]
    public async Task IsPr_marks_only_sessions_that_strictly_exceed_the_running_max()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // e1RMs over four weeks: 116.7, 116.7 (tie ⇒ not a PR), 122.5 (new best), 116.7 (below ⇒ not a PR).
        var weights = new (int weeksAgo, int reps, decimal weight)[]
        {
            (3, 5, 100m),   // 116.7  → PR (first)
            (2, 5, 100m),   // 116.7  → tie, NOT a PR
            (1, 5, 105m),   // 122.5  → new best, PR
            (0, 5, 100m),   // 116.7  → below, NOT a PR
        };
        var sessions = weights
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(w.reps, w.weight))))
            .ToList();

        var result = await Run(userId, lift, sessions);

        var points = result.Value!.Points;
        Assert.Equal(4, points.Count);
        // Points come back oldest → newest.
        Assert.True(points[0].IsPr);    // first qualifying session is always a PR
        Assert.False(points[1].IsPr);   // exact tie does not set a new PR
        Assert.True(points[2].IsPr);    // strictly higher → PR
        Assert.False(points[3].IsPr);   // below the running max → not a PR
    }

    // ── shared-calculator parity with the overview ──

    [Fact]
    public async Task Strength_summary_matches_the_shared_calculator_over_the_same_series()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Five weekly sessions, the newest a clear jump → the handler's Current/Delta/Direction/Stall must
        // equal what E1rmSeriesCalculator produces over the same MAX-per-session series.
        var weeks = new (int weeksAgo, decimal weight)[]
        {
            (4, 100m), (3, 100m), (2, 100m), (1, 100m), (0, 120m)
        };
        var sessions = weeks
            .Select(w => CompletedSession(userId, MondayInstant(w.weeksAgo),
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();

        var result = await Run(userId, lift, sessions);
        var dto = result.Value!;

        // Rebuild the expected trend straight from the calculator using the DTO's own session-best series.
        var expectedPoints = dto.Points
            .Select((p, i) => new E1rmSeriesCalculator.Point(
                MondayOfWeek(p.Date), i, p.SessionBestE1rmKg))
            .ToList();
        var expected = E1rmSeriesCalculator.Compute(expectedPoints);

        Assert.Equal(expected.CurrentE1rmKg, dto.CurrentE1rmKg);
        Assert.Equal(expected.DeltaKgVsTrailing4w, dto.DeltaKgVsTrailing4w);
        Assert.Equal(expected.Direction, dto.Direction);
        Assert.Equal(expected.Stalled, dto.Stalled);
        Assert.Equal(expected.StallSessions, dto.StallSessions);
        // And it is a real Up trend (sanity that the parity isn't trivially flat==flat).
        Assert.Equal(LiftTrendDirection.Up, dto.Direction);
    }

    private static DateOnly MondayOfWeek(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    // ── self-scope / IDOR ──

    [Fact]
    public async Task Read_only_queries_the_callers_own_sessions()
    {
        var userA = Guid.NewGuid();
        var lift = Guid.NewGuid();
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.QueryOwnAcrossGyms(userA)
            .Returns(new TestAsyncEnumerable<WorkoutSession>(
            [
                CompletedSession(userA, MondayInstant(0),
                    Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m)))
            ]));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userA);
        currentUser.TimeZoneId.Returns("UTC");

        var sut = new GetMyExerciseE1rmSeriesHandler(repo, currentUser);
        var result = await sut.Handle(new GetMyExerciseE1rmSeriesQuery(lift, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The repository is only ever asked for the caller's own id — user B's sessions are unreachable.
        repo.Received(1).QueryOwnAcrossGyms(userA);
        repo.DidNotReceive().QueryOwnAcrossGyms(Arg.Is<Guid>(id => id != userA));
    }

    // ── windowing ──

    [Fact]
    public async Task From_bound_excludes_sessions_before_the_requested_window()
    {
        var userId = Guid.NewGuid();
        var lift = Guid.NewGuid();

        var sessions = new[]
        {
            CompletedSession(userId, MondayInstant(8),   // 8 weeks ago — before a 4-week `from`
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 90m))),
            CompletedSession(userId, MondayInstant(1),   // within window
                Exercise(lift, "Lift", ExerciseTrackingType.Strength, WorkingSet(5, 100m))),
        };

        // from = this week's Monday minus 4 weeks → the 8-weeks-ago session is excluded.
        var from = ThisMonday.AddDays(-7 * 4);
        var result = await Run(userId, lift, sessions, from: from);

        var point = Assert.Single(result.Value!.Points);
        Assert.Equal(116.7m, point.SessionBestE1rmKg);   // only the in-window 100×5 survived
    }
}
