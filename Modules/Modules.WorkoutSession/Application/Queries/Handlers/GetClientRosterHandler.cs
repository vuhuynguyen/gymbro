using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// The coach needs-attention roster (api/clients/progress/roster, Phase 2b), TENANT-SCOPED to the active gym.
/// Gated on <c>WorkoutLogViewAll</c> for the active tenant; every session read goes through
/// <see cref="IWorkoutSessionRepository.Query"/> with the EF tenant filter ON, so a client who trains across
/// gyms is seen here ONLY by their in-gym sessions (FEASIBILITY R2 — never <c>QueryOwnAcrossGyms</c>).
///
/// <para>Per member of the active tenant: LastActiveAt = MAX(StartedAt) in this gym; CompletedThisWeek =
/// completed sessions this Monday-week (bucketed in the CLIENT's zone); WeeklyGoal = the member's in-gym
/// active <c>PlanAssignment.FrequencyDaysPerWeek</c>; AdherencePct = weeks-hitting-goal ÷ weeks-observed over
/// the in-gym 12-week window. Status is computed from CHEAP signals only (Decision D4 — no per-lift stall at
/// roster scale): Quiet when no session in <see cref="QuietGapDays"/> days, Drifting when adherence falls
/// below <see cref="AdherenceBandPct"/>, else OnTrack. Rows are sorted at-risk-first.</para>
///
/// Only members with at least one in-gym session appear; returns an empty-but-valid roster (200, never 204)
/// when the gym has no members with sessions.
/// </summary>
public sealed class GetClientRosterHandler(
    IWorkoutSessionRepository sessionRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<GetClientRosterQuery, Result<RosterDto>>
{
    private const int WindowWeeks = 12;
    // D4 cheap-signal thresholds:
    private const int QuietGapDays = 10;       // no completed session in N days ⇒ at risk of churning
    private const int AdherenceBandPct = 75;   // weekly adherence below this band ⇒ slipping off plan

    public async Task<Result<RosterDto>> Handle(GetClientRosterQuery request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
            return Result<RosterDto>.Failure(Validation("TenantId", "X-Tenant-Id header is required."));

        // Coach gate: only a caller who can view ALL workout logs in this gym may read the roster. A plain
        // member (WorkoutLogViewOwn only) is forbidden — the roster is a coach-only surface.
        if (!await tenantAuth.HasPermissionAsync(tenantId, Permission.WorkoutLogViewAll, cancellationToken))
            return Result<RosterDto>.Failure(Forbidden("Forbidden", "You cannot view this gym's client roster."));

        var membersResult = await mediator.Send(new ResolveTenantMemberNamesQuery(tenantId), cancellationToken);
        if (membersResult.IsFailure)
            return Result<RosterDto>.Failure(membersResult.Error);

        var members = membersResult.Value!;
        if (members.Count == 0)
            return Result<RosterDto>.Success(new RosterDto([]));

        var memberIds = members.Select(m => m.UserId).ToList();

        var now = DateTimeOffset.UtcNow;
        // Bound the scan to the 12-week window (conservative one-day slack for cross-zone timestamps); exact
        // local-week bucketing is in memory, per the client's zone.
        var windowLowerBoundUtc = now.AddDays(-7 * WindowWeeks - 1);

        // TENANT-FILTERED read (filter ON): completed sessions in THIS gym for these members, in window.
        // Projected to a flat shape so the LINQ translates; bucketing happens in memory.
        var rows = await sessionRepository.Query()
            .Where(s => s.Status == SessionStatus.Completed
                && memberIds.Contains(s.TraineeId)
                && s.StartedAt >= windowLowerBoundUtc)
            .Select(s => new { s.TraineeId, s.StartedAt, s.ClientTimezone })
            .ToListAsync(cancellationToken);

        // Last-active is the MAX StartedAt over ALL in-gym completed sessions (not just the window), so a
        // long-quiet client still gets an honest "last active" instant.
        var lastActiveByTrainee = await sessionRepository.Query()
            .Where(s => s.Status == SessionStatus.Completed && memberIds.Contains(s.TraineeId))
            .GroupBy(s => s.TraineeId)
            .Select(g => new { TraineeId = g.Key, LastActiveAt = g.Max(s => s.StartedAt) })
            .ToListAsync(cancellationToken);

        var lastActiveMap = lastActiveByTrainee.ToDictionary(x => x.TraineeId, x => x.LastActiveAt);

        // In-gym active-assignment goal per member, resolved by the WorkoutPlan module with the EF tenant
        // filter ON (own gym only) — never a cross-gym goal. Cross-module via a MediatR contract so this
        // handler doesn't reach into WorkoutPlan's entity namespace (module-boundary rule).
        var goalsResult = await mediator.Send(
            new ResolveActiveAssignmentGoalsQuery(memberIds), cancellationToken);
        var goalMap = goalsResult.IsSuccess
            ? goalsResult.Value!
            : new Dictionary<Guid, int>();

        var rowsByTrainee = rows
            .GroupBy(r => r.TraineeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<ClientStatusDto>(members.Count);
        foreach (var member in members)
        {
            // Only surface members who have actually trained in THIS gym (any in-gym completed session).
            if (!lastActiveMap.TryGetValue(member.UserId, out var lastActiveAt))
                continue;

            // Bucket this member's in-gym weeks in the member's OWN zone (stored zone → per-session captured
            // zone → UTC), never the coach's. The zone arrives folded into the member row from the names
            // query, so there's no per-member round-trip (the roster stays a fixed number of reads).
            var memberZone = member.TimeZoneId;
            var currentWeekStart = LocalDayResolver.WeekStartOf(now, memberZone);
            var windowStart = currentWeekStart.AddDays(-7 * (WindowWeeks - 1));

            var memberRows = rowsByTrainee.TryGetValue(member.UserId, out var mr) ? mr : [];

            var weekBuckets = memberRows
                .Select(r => LocalDayResolver.WeekStartOf(r.StartedAt, r.ClientTimezone ?? memberZone))
                .Where(w => w >= windowStart && w <= currentWeekStart)
                .ToList();

            var completedThisWeek = weekBuckets.Count(w => w == currentWeekStart);

            int? goal = goalMap.TryGetValue(member.UserId, out var g) ? g : null;
            var adherencePct = ComputeAdherencePct(weekBuckets, currentWeekStart, goal);

            var status = Classify(now, lastActiveAt, adherencePct);

            items.Add(new ClientStatusDto(
                member.UserId,
                member.DisplayName,
                lastActiveAt,
                completedThisWeek,
                goal,
                adherencePct,
                status));
        }

        // At-risk first: Quiet, then Drifting, then OnTrack. Within a status, the longest-quiet client leads
        // (oldest last-active first); then a stable name sort so the order is deterministic.
        var ordered = items
            .OrderBy(i => StatusRank(i.Status))
            .ThenBy(i => i.LastActiveAt ?? DateTimeOffset.MinValue)
            .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<RosterDto>.Success(new RosterDto(ordered));
    }

    // Weeks-hitting-goal ÷ weeks-observed over the in-gym window (weeks-observed from the first in-gym session
    // week through the current week — forgiving for newcomers). Null when there is no goal or no sessions.
    private static int? ComputeAdherencePct(
        IReadOnlyList<DateOnly> weekBuckets, DateOnly currentWeekStart, int? goal)
    {
        if (goal is not int weeklyGoal || weeklyGoal <= 0 || weekBuckets.Count == 0)
            return null;

        var sessionsByWeek = weekBuckets
            .GroupBy(w => w)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        var firstWeek = sessionsByWeek.Keys.Min();
        var observed = 0;
        var hitting = 0;
        for (var w = firstWeek; w <= currentWeekStart; w = w.AddDays(7))
        {
            observed++;
            if (sessionsByWeek.GetValueOrDefault(w) >= weeklyGoal)
                hitting++;
        }

        return observed == 0
            ? null
            : (int)Math.Round(100m * hitting / observed, MidpointRounding.AwayFromZero);
    }

    // D4 cheap-signal triage: a quiet gap dominates (churn risk), then an adherence band miss, else OnTrack.
    // No per-lift "Stalled" leg at roster scale (resolved on client-open).
    private static RosterStatus Classify(DateTimeOffset now, DateTimeOffset lastActiveAt, int? adherencePct)
    {
        if ((now - lastActiveAt).TotalDays >= QuietGapDays)
            return RosterStatus.Quiet;

        if (adherencePct is int pct && pct < AdherenceBandPct)
            return RosterStatus.Drifting;

        return RosterStatus.OnTrack;
    }

    private static int StatusRank(RosterStatus status) => status switch
    {
        RosterStatus.Quiet => 0,
        RosterStatus.Drifting => 1,
        _ => 2
    };
}
