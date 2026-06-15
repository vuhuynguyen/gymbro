using System.Reflection;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The coach roster math (api/clients/progress/roster, Phase 2b), fully mocked — no database. Pins the frozen
/// rules from API-CONTRACTS §4 + COACH-VS-TRAINEE.md: the roster is TENANT-SCOPED (the handler reads the
/// tenant-filtered <c>Query()</c>, NEVER <c>QueryOwnAcrossGyms</c> — R2); it is gated on
/// <c>WorkoutLogViewAll</c> (a plain member is forbidden); the status chip uses CHEAP signals only (Quiet at
/// the 10-day gap, Drifting below the 75% adherence band, else OnTrack — no "Stalled" at roster scale, D4);
/// and rows are sorted at-risk-first. Time-relative to UtcNow (no calendar time-bomb).
/// </summary>
public sealed class GetClientRosterHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static readonly DateTimeOffset NowUtc = DateTimeOffset.UtcNow;
    private static readonly DateOnly ThisMonday = MondayOfUtcWeek(NowUtc);

    private static readonly DateTimeOffset ThisMondayInstant =
        new(ThisMonday.Year, ThisMonday.Month, ThisMonday.Day, 9, 0, 0, TimeSpan.Zero);

    private static DateOnly MondayOfUtcWeek(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }

    // A 09:00-UTC instant `weeksAgo` weeks before this Monday (0 = this week's Monday).
    private static DateTimeOffset WeekInstant(int weeksAgo) => ThisMondayInstant.AddDays(-7 * weeksAgo);

    // ── handler wiring ──

    private sealed record Member(Guid Id, string Name);

    private static GetClientRosterHandler CreateSut(
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyList<Member> members,
        IReadOnlyDictionary<Guid, int>? goals = null,
        bool hasViewAll = true,
        Action<IWorkoutSessionRepository>? trackRepo = null)
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.Query().Returns(new TestAsyncEnumerable<WorkoutSession>(sessions));
        // Guard: the roster must NEVER reach for the cross-gym path. If it does, return a poisoned (empty) set,
        // so a regression flips the result and the only-call assertions below catch it.
        repo.QueryOwnAcrossGyms(Arg.Any<Guid>())
            .Returns(new TestAsyncEnumerable<WorkoutSession>(Array.Empty<WorkoutSession>()));
        trackRepo?.Invoke(repo);

        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        tenantAuth.HasPermissionAsync(Tenant, Permission.WorkoutLogViewAll, Arg.Any<CancellationToken>())
            .Returns(hasViewAll);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Tenant);

        var mediator = Substitute.For<IMediator>();
        // Every member's zone is UTC for these tests — folded into the member row (no per-member zone query).
        mediator.Send(Arg.Any<ResolveTenantMemberNamesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TenantMemberNameDto>>.Success(
                members.Select(m => new TenantMemberNameDto(m.Id, m.Name, "UTC")).ToList()));
        // The in-gym goal lookup (tenant-filtered) is resolved cross-module; mock its dictionary directly.
        mediator.Send(Arg.Any<ResolveActiveAssignmentGoalsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyDictionary<Guid, int>>.Success(
                goals ?? new Dictionary<Guid, int>()));

        return new GetClientRosterHandler(repo, tenantAuth, tenantContext, mediator);
    }

    private static Task<Result<RosterDto>> Run(
        IEnumerable<WorkoutSession> sessions,
        IReadOnlyList<Member> members,
        IReadOnlyDictionary<Guid, int>? goals = null,
        bool hasViewAll = true)
        => CreateSut(sessions, members, goals, hasViewAll)
            .Handle(new GetClientRosterQuery(), CancellationToken.None);

    // ── entity seeding (private setters + UtcNow stamps ⇒ reflection) ──

    private static WorkoutSession CompletedSession(Guid traineeId, DateTimeOffset startedAt)
    {
        var session = WorkoutSession.Start(
            traineeId, Tenant, SessionSource.Adhoc, null, null, "Lift", null, "UTC", null);
        SetProp(session, "StartedAt", startedAt);
        SetProp(session, "Status", SessionStatus.Completed);
        return session;
    }

    private static Dictionary<Guid, int> Goal(Guid traineeId, int frequency)
        => new() { [traineeId] = frequency };

    private static void SetProp(object target, string name, object value)
        => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(target, value);

    // ── auth ──

    [Fact]
    public async Task Member_without_view_all_is_forbidden()
    {
        var client = Guid.NewGuid();
        var result = await Run(
            [CompletedSession(client, WeekInstant(0))],
            [new Member(client, "Cara")],
            hasViewAll: false);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Missing_tenant_header_is_a_validation_failure()
    {
        var repo = Substitute.For<IWorkoutSessionRepository>();
        var tenantAuth = Substitute.For<ITenantAuthorizationService>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);
        var mediator = Substitute.For<IMediator>();

        var sut = new GetClientRosterHandler(repo, tenantAuth, tenantContext, mediator);
        var result = await sut.Handle(new GetClientRosterQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BuildingBlocks.Shared.Errors.ErrorType.Validation, result.Error.Type);
    }

    // ── tenant-scoping (R2): the roster reads the tenant-filtered Query(), never QueryOwnAcrossGyms ──

    [Fact]
    public async Task Roster_reads_tenant_filtered_query_and_never_the_cross_gym_path()
    {
        var client = Guid.NewGuid();
        var sessions = new[] { CompletedSession(client, WeekInstant(0)) };

        IWorkoutSessionRepository? captured = null;
        var sut = CreateSut(sessions, [new Member(client, "Cara")], trackRepo: r => captured = r);
        var result = await sut.Handle(new GetClientRosterQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The tenant-filtered Query() is the ONLY session source; the cross-gym bypass is never touched.
        captured!.Received().Query();
        captured!.DidNotReceive().QueryOwnAcrossGyms(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Empty_items_when_no_members_have_sessions()
    {
        var client = Guid.NewGuid();
        // Member exists, but has NO completed session in this gym → excluded from the roster.
        var result = await Run([], [new Member(client, "Cara")]);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    // ── status classification at the thresholds ──

    [Fact]
    public async Task Quiet_when_no_session_within_the_gap_threshold()
    {
        var client = Guid.NewGuid();
        // Last session ~12 days ago (> 10-day quiet gap) → Quiet, regardless of adherence.
        var session = CompletedSession(client, NowUtc.AddDays(-12));
        var result = await Run(
            [session],
            [new Member(client, "Quincy")],
            Goal(client, 1));

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal(RosterStatus.Quiet, row.Status);
    }

    [Fact]
    public async Task On_track_when_recently_active_and_meeting_goal()
    {
        var client = Guid.NewGuid();
        // Goal 1/wk, one completed session every week for the last 4 weeks (incl. this week) → adherence 100%,
        // last-active within the gap → OnTrack.
        var sessions = Enumerable.Range(0, 4)
            .Select(w => CompletedSession(client, WeekInstant(w)))
            .ToList();

        var result = await Run(sessions, [new Member(client, "Tina")], Goal(client, 1));

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal(RosterStatus.OnTrack, row.Status);
        Assert.Equal(100, row.AdherencePct);
        Assert.Equal(1, row.CompletedThisWeek);
        Assert.Equal(1, row.WeeklyGoal);
    }

    [Fact]
    public async Task Drifting_when_recently_active_but_below_the_adherence_band()
    {
        var client = Guid.NewGuid();
        // Goal 3/wk. Recently active (a session THIS week, so not Quiet), but only one of the last four weeks
        // hit the goal → 25% adherence, below the 75% band → Drifting.
        var sessions = new List<WorkoutSession>
        {
            // Week 0: three sessions (hits the 3/wk goal) and keeps last-active recent.
            CompletedSession(client, WeekInstant(0)),
            CompletedSession(client, WeekInstant(0).AddDays(1)),
            CompletedSession(client, WeekInstant(0).AddDays(2)),
            // Weeks 1..3: one session each (misses the 3/wk goal).
            CompletedSession(client, WeekInstant(1)),
            CompletedSession(client, WeekInstant(2)),
            CompletedSession(client, WeekInstant(3)),
        };

        var result = await Run(sessions, [new Member(client, "Drew")], Goal(client, 3));

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal(RosterStatus.Drifting, row.Status);
        Assert.Equal(25, row.AdherencePct);   // 1 of 4 observed weeks hit the goal
    }

    [Fact]
    public async Task No_active_plan_yields_null_goal_and_null_adherence_but_still_classifies_by_quiet()
    {
        var client = Guid.NewGuid();
        // No assignment → no goal/adherence. Recently active → not Quiet → OnTrack (Drifting needs a goal).
        var result = await Run([CompletedSession(client, WeekInstant(0))], [new Member(client, "Nora")]);

        var row = Assert.Single(result.Value!.Items);
        Assert.Null(row.WeeklyGoal);
        Assert.Null(row.AdherencePct);
        Assert.Equal(RosterStatus.OnTrack, row.Status);
    }

    // ── sort order (at-risk first) ──

    [Fact]
    public async Task Rows_are_sorted_quiet_then_drifting_then_on_track()
    {
        var quiet = Guid.NewGuid();
        var drifting = Guid.NewGuid();
        var onTrack = Guid.NewGuid();

        var sessions = new List<WorkoutSession>
        {
            // Quiet: last active 15 days ago.
            CompletedSession(quiet, NowUtc.AddDays(-15)),
            // Drifting: active this week but missing a 3/wk goal across weeks.
            CompletedSession(drifting, WeekInstant(0)),
            CompletedSession(drifting, WeekInstant(1)),
            CompletedSession(drifting, WeekInstant(2)),
            CompletedSession(drifting, WeekInstant(3)),
            // OnTrack: 1/wk goal, met every week.
            CompletedSession(onTrack, WeekInstant(0)),
            CompletedSession(onTrack, WeekInstant(1)),
            CompletedSession(onTrack, WeekInstant(2)),
            CompletedSession(onTrack, WeekInstant(3)),
        };

        var members = new[]
        {
            new Member(onTrack, "OnTrack"),
            new Member(drifting, "Drifting"),
            new Member(quiet, "Quiet"),
        };

        var goals = new Dictionary<Guid, int>
        {
            [drifting] = 3,
            [onTrack] = 1,
        };

        var result = await Run(sessions, members, goals);

        var items = result.Value!.Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(RosterStatus.Quiet, items[0].Status);
        Assert.Equal(RosterStatus.Drifting, items[1].Status);
        Assert.Equal(RosterStatus.OnTrack, items[2].Status);
    }
}
