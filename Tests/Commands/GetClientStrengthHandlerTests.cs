using System.Reflection;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
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
/// The coach per-client e1RM trends (api/clients/{id}/progress/strength, Phase 2b), fully mocked — no database.
/// Pins the frozen rules from API-CONTRACTS §4 + COACH-VS-TRAINEE.md §4 (R2 isolation): it is a SEPARATE,
/// TENANT-SCOPED handler that reads the tenant-filtered <c>Query()</c> and NEVER <c>QueryOwnAcrossGyms</c>;
/// it gates on <c>WorkoutLogViewAll</c> via ResourceAccessGuard (a plain member is forbidden) and requires the
/// trainee to be a member of the active tenant (a non-member → 404, never a silent rescope to self); it applies
/// the same honesty gate + MAX-per-session reduction as the trainee series and reuses the shared
/// <see cref="E1rmSeriesCalculator"/>. Time-relative to UtcNow.
/// </summary>
public sealed class GetClientStrengthHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Coach = Guid.NewGuid();

    private static readonly DateOnly ThisMonday = MondayOfUtcWeek(DateTimeOffset.UtcNow);

    private static readonly DateTimeOffset ThisMondayInstant =
        new(ThisMonday.Year, ThisMonday.Month, ThisMonday.Day, 9, 0, 0, TimeSpan.Zero);

    private static DateOnly MondayOfUtcWeek(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    private static DateTimeOffset WeekInstant(int weeksAgo) => ThisMondayInstant.AddDays(-7 * weeksAgo);

    // ── handler wiring ──

    private static GetClientStrengthHandler CreateSut(
        IEnumerable<WorkoutSession> sessions,
        bool coachHasViewAll = true,
        bool traineeIsMember = true,
        Action<IWorkoutSessionRepository>? trackRepo = null)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.Query().Returns(new TestAsyncEnumerable<WorkoutSession>(sessions));
        repo.QueryOwnAcrossGyms(Arg.Any<Guid>())
            .Returns(new TestAsyncEnumerable<WorkoutSession>(Array.Empty<WorkoutSession>()));
        trackRepo?.Invoke(repo);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Coach);

        // ResourceAccessGuard → CanAccessResourceAsync. With WorkoutLogViewAll for the active tenant + the
        // resource living in that same tenant, the coach is permitted; without it, denied. (The real service's
        // semantics; we mock the boolean directly since the handler only consumes the guard's verdict.)
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        tenantAuth.CanAccessResourceAsync(
                Tenant, Permission.WorkoutLogViewOwn, Permission.WorkoutLogViewAll,
                Arg.Any<Guid>(), Tenant, Arg.Any<CancellationToken>())
            .Returns(coachHasViewAll);

        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(Arg.Any<Guid>(), Tenant, Arg.Any<CancellationToken>())
            .Returns(traineeIsMember ? TenantRole.Client : (TenantRole?)null);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Tenant);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetUserTimeZoneQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("UTC"));

        return new GetClientStrengthHandler(repo, tenantAuth, roleResolver, tenantContext, mediator);
    }

    private static Task<Result<IReadOnlyList<LiftTrendDto>>> Run(
        Guid traineeId,
        IEnumerable<WorkoutSession> sessions,
        int take = 6,
        bool coachHasViewAll = true,
        bool traineeIsMember = true)
        => CreateSut(sessions, coachHasViewAll, traineeIsMember)
            .Handle(new GetClientStrengthQuery(traineeId, take), CancellationToken.None);

    // ── entity seeding ──

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

    // Four weekly sessions on one lift, ramping the last week → a clear Up trend that clears the ≥4 bar.
    private static List<WorkoutSession> FourWeeklyBenchSessions(Guid traineeId, Guid lift)
    {
        var weeks = new (int weeksAgo, decimal weight)[] { (3, 100m), (2, 100m), (1, 100m), (0, 120m) };
        return weeks
            .Select(w => CompletedSession(traineeId, WeekInstant(w.weeksAgo),
                Exercise(lift, "Bench", ExerciseTrackingType.Strength, WorkingSet(5, w.weight))))
            .ToList();
    }

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    private static List<T> Backing<T>(object target, string field)
        => (List<T>)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(target)!;

    // ── auth ──

    [Fact]
    public async Task Coach_without_view_all_is_forbidden()
    {
        var client = Guid.NewGuid();
        var lift = Guid.NewGuid();
        var result = await Run(client, FourWeeklyBenchSessions(client, lift), coachHasViewAll: false);

        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Non_member_trainee_is_not_found_never_rescoped_to_self()
    {
        var nonMember = Guid.NewGuid();
        var lift = Guid.NewGuid();
        // The coach HAS ViewAll, but the requested id is not a member of the active tenant → 404.
        var result = await Run(nonMember, FourWeeklyBenchSessions(nonMember, lift), traineeIsMember: false);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Missing_tenant_header_is_a_validation_failure()
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var roleResolver = Substitute.For<ITenantRoleResolver>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);
        var mediator = Substitute.For<IMediator>();

        var sut = new GetClientStrengthHandler(repo, tenantAuth, roleResolver, tenantContext, mediator);
        var result = await sut.Handle(new GetClientStrengthQuery(Guid.NewGuid(), 6), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BuildingBlocks.Shared.Errors.ErrorType.Validation, result.Error.Type);
    }

    // ── tenant-scoping (R2) ──

    [Fact]
    public async Task Reads_tenant_filtered_query_and_never_the_cross_gym_path()
    {
        var client = Guid.NewGuid();
        var lift = Guid.NewGuid();

        IWorkoutSessionRepository? captured = null;
        var sut = CreateSut(FourWeeklyBenchSessions(client, lift), trackRepo: r => captured = r);
        var result = await sut.Handle(new GetClientStrengthQuery(client, 6), CancellationToken.None);

        Assert.True(result.IsSuccess);
        captured!.Received().Query();
        captured!.DidNotReceive().QueryOwnAcrossGyms(Arg.Any<Guid>());
    }

    // ── trend build + honesty gate + parity ──

    [Fact]
    public async Task Builds_top_lift_trend_with_shared_calculator_parity()
    {
        var client = Guid.NewGuid();
        var lift = Guid.NewGuid();

        var result = await Run(client, FourWeeklyBenchSessions(client, lift));

        Assert.True(result.IsSuccess);
        var trend = Assert.Single(result.Value!);
        Assert.Equal(lift, trend.ExerciseId);
        Assert.Equal("Strength", trend.TrackingType);
        Assert.Equal(LiftTrendDirection.Up, trend.Direction);

        // Parity: rebuild the expected trend from the shared calculator over the same MAX-per-session series.
        var expectedPoints = trend.SparkE1rmKg
            .Select((e, i) => new E1rmSeriesCalculator.Point(ThisMonday.AddDays(-7 * (3 - i)), i, e))
            .ToList();
        var expected = E1rmSeriesCalculator.Compute(expectedPoints);
        Assert.Equal(expected.CurrentE1rmKg, trend.CurrentE1rmKg);
        Assert.Equal(expected.Direction, trend.Direction);
    }

    [Fact]
    public async Task Lift_with_fewer_than_four_sessions_is_omitted()
    {
        var client = Guid.NewGuid();
        var lift = Guid.NewGuid();
        // Only 3 weekly sessions → below the ≥4-session top-lift bar → omitted.
        var sessions = new[] { 2, 1, 0 }
            .Select(w => CompletedSession(client, WeekInstant(w),
                Exercise(lift, "Bench", ExerciseTrackingType.Strength, WorkingSet(5, 100m))))
            .ToList();

        var result = await Run(client, sessions);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Honesty_gate_excludes_high_rep_non_working_and_non_strength_rows()
    {
        var client = Guid.NewGuid();
        var lift = Guid.NewGuid();

        // Each of four weekly sessions has the qualifying 100×5 (e1RM 116.7) plus noise that must be ignored.
        var sessions = Enumerable.Range(0, 4)
            .Select(w => CompletedSession(client, WeekInstant(w),
                Exercise(lift, "Bench", ExerciseTrackingType.Strength,
                    WorkingSet(5, 100m),
                    PerformedSet.Log(Guid.NewGuid(), Tenant, null, 2, PerformedSetType.Working,
                        20, 200m, null, null, null, null, true),   // reps > 12 — excluded
                    PerformedSet.Log(Guid.NewGuid(), Tenant, null, 3, PerformedSetType.Warmup,
                        3, 300m, null, null, null, null, true))))   // warmup — excluded
            .ToList();

        var result = await Run(client, sessions);

        var trend = Assert.Single(result.Value!);
        // The session best is the qualifying 100×5 (116.7), never the 200×20 or the 300×3 warmup.
        Assert.Equal(116.7m, trend.CurrentE1rmKg);
        Assert.Equal(116.7m, trend.SparkE1rmKg[^1]);
    }
}
