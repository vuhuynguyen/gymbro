using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// The single-call trainee Progress home. Self-scoped via
/// <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/> (<c>currentUser.UserId</c> only) across every
/// gym the caller trains in; only completed sessions count. Computes, over a user-selectable Monday-anchored
/// window (<see cref="GetMyProgressOverviewQuery.Weeks"/>, clamped to [4, 52], default 12) in the trainee's
/// zone: current-week adherence against the authoritative active-plan goal (Decision D1, resolved via an
/// internal <see cref="GetOwnActiveAssignmentsQuery"/>), daily consistency + streak, top-3 honesty-gated lift
/// e1RM directions, and a PR teaser (top 3 from <see cref="GetMyPersonalRecordsQuery"/>). The This-Week hero,
/// the trailing-4-week strength baseline, and the 3-exposure stall stay fixed regardless of the window.
/// One bounded read materializes the window, then all aggregation is done in memory; returns an
/// empty-but-valid DTO (never a failure) for a brand-new user.
/// </summary>
public sealed class GetMyProgressOverviewHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyProgressOverviewQuery, Result<ProgressOverviewDto>>
{
    private const int DefaultWindowWeeks = 12;
    private const int MinWindowWeeks = 4;
    private const int MaxWindowWeeks = 52;
    // Shared with the full strength-lift list so the top-3 strip and the lift list can never diverge on the
    // honesty gate — both omit/flag lifts below the same qualifying-session count.
    private const int MinSessionsForTopLift = StrengthLiftSeries.MinSessionsForTrend;
    private const int MaxTopLifts = 3;
    // Direction/stall/trailing-baseline constants AND the spark-point cap live in E1rmSeriesCalculator
    // (the shared math) — Spark defaults to E1rmSeriesCalculator.DefaultSparkPoints.

    public async Task<Result<ProgressOverviewDto>> Handle(
        GetMyProgressOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // The user-selectable window (consistency window + heatmap span + top-lift gathering), clamped to
        // [4, 52], default 12 when null. The This-Week hero/goal, the trailing-4-week strength baseline, and
        // the 3-exposure stall are NOT driven by this — they stay fixed regardless of the selected window.
        var windowWeeks = Math.Clamp(request.Weeks ?? DefaultWindowWeeks, MinWindowWeeks, MaxWindowWeeks);
        // The trainee's own stored zone anchors "this week" and the selected window; per-session captured
        // zones still decide each session's own local day below.
        var userZone = currentUser.TimeZoneId;
        var currentWeekStart = LocalDayResolver.WeekStartOf(now, userZone);
        var windowStart = currentWeekStart.AddDays(-7 * (windowWeeks - 1));

        // Conservative UTC lower bound (one day of slack for cross-zone session timestamps) keeps the scan
        // bounded; exact local-day/week bucketing happens in memory once materialized.
        var windowLowerBoundUtc = LocalDayResolver
            .StartOfLocalDayUtc(windowStart, userZone)
            .AddDays(-1);

        // One bounded read. Projected to anonymous shapes (mirrors GetMyProgressHandler) so the LINQ
        // translates cleanly; the honesty gate lives here in SQL, all bucketing/aggregation is in memory.
        var raw = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .Where(s => s.Status == SessionStatus.Completed && s.StartedAt >= windowLowerBoundUtc)
            .Select(s => new
            {
                s.StartedAt,
                s.ClientTimezone,
                s.TenantId,
                // Honesty gate, applied server-side: only Working sets with an e1RM, reps ≤ 12, on a
                // strength/bodyweight lift contribute. (e1RM is populated regardless of reps, so the
                // Reps ≤ 12 cap must be enforced here.)
                Lifts = s.Exercises
                    .Where(e => e.TrackingType == ExerciseTrackingType.Strength
                        || e.TrackingType == ExerciseTrackingType.Bodyweight)
                    .Select(e => new
                    {
                        e.ExerciseId,
                        e.ExerciseName,
                        // MAX e1RM over this session's qualifying working sets = one point per (lift, session).
                        // Drop/AMRAP/failure stages carry null e1RM, so MAX is cluster-safe (no ParentSetId filter).
                        BestE1rmKg = e.Sets
                            .Where(set => set.SetType == PerformedSetType.Working
                                && set.EstimatedOneRepMaxKg != null
                                && set.Reps != null && set.Reps <= SessionPrRules.MaxPrReps)
                            .Max(set => (decimal?)set.EstimatedOneRepMaxKg)
                    })
            })
            .ToListAsync(cancellationToken);

        // Map to the named in-memory shape, dropping lift points whose e1RM is null (a session with no
        // qualifying working set on that lift contributes nothing to its series).
        var rows = raw
            .Select(s => new SessionRow
            {
                StartedAt = s.StartedAt,
                ClientTimezone = s.ClientTimezone,
                TenantId = s.TenantId,
                Lifts = s.Lifts
                    .Where(l => l.BestE1rmKg is not null)
                    .Select(l => new LiftRow
                    {
                        ExerciseId = l.ExerciseId,
                        ExerciseName = l.ExerciseName,
                        BestE1rmKg = l.BestE1rmKg
                    })
                    .ToList()
            })
            .ToList();

        // Re-bucket each session to its own local Monday week, then drop anything outside the selected window.
        var windowRows = rows
            .Select(r => new
            {
                Row = r,
                LocalDate = LocalDayResolver.LocalDateOf(r.StartedAt, r.ClientTimezone ?? userZone),
                WeekStart = LocalDayResolver.WeekStartOf(r.StartedAt, r.ClientTimezone ?? userZone)
            })
            .Where(x => x.WeekStart >= windowStart && x.WeekStart <= currentWeekStart)
            .ToList();

        var goal = await ResolveGoalAsync(windowRows.Select(x => x.Row), currentWeekStart, cancellationToken);

        var thisWeek = new WeekAdherenceDto(
            currentWeekStart,
            windowRows.Count(x => x.WeekStart == currentWeekStart),
            goal,
            HasActivePlan: goal is not null);

        var consistency = BuildConsistency(
            windowRows.Select(x => (x.LocalDate, x.WeekStart)),
            currentWeekStart,
            goal,
            windowWeeks);

        var topLifts = BuildTopLifts(
            windowRows.Select(x => (x.WeekStart, x.Row)));

        var recentPrs = await ResolveRecentPrsAsync(cancellationToken);

        return Result<ProgressOverviewDto>.Success(new ProgressOverviewDto(
            thisWeek,
            consistency,
            topLifts,
            recentPrs,
            GeneratedAtUtc: now));
    }

    // ── D1: the active assignment whose gym has the most completed sessions THIS week, tie-broken by latest
    //    StartDate. No active assignment ⇒ null goal (client hides the ring). ──
    private async Task<int?> ResolveGoalAsync(
        IEnumerable<SessionRow> windowRows,
        DateOnly currentWeekStart,
        CancellationToken cancellationToken)
    {
        var assignmentsResult = await mediator.Send(
            new GetOwnActiveAssignmentsQuery(currentUser.UserId), cancellationToken);
        if (assignmentsResult.IsFailure)
            return null;

        var assignments = assignmentsResult.Value!;
        if (assignments.Count == 0)
            return null;

        // Completed sessions per gym in the current week (only gyms that have an active assignment matter).
        var completedThisWeekByTenant = windowRows
            .Where(r => r.TenantId != null
                && LocalDayResolver.WeekStartOf(r.StartedAt, r.ClientTimezone ?? currentUser.TimeZoneId) == currentWeekStart)
            .GroupBy(r => r.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var authoritative = assignments
            .OrderByDescending(a => completedThisWeekByTenant.GetValueOrDefault(a.TenantId))
            .ThenByDescending(a => a.StartDate)
            .First();

        return authoritative.FrequencyDaysPerWeek;
    }

    private static ConsistencyDto BuildConsistency(
        IEnumerable<(DateOnly LocalDate, DateOnly WeekStart)> rows,
        DateOnly currentWeekStart,
        int? goal,
        int windowWeeks)
    {
        var materialized = rows.ToList();

        var days = materialized
            .GroupBy(x => x.LocalDate)
            .Select(g => new ConsistencyDayDto(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        // Completed sessions per Monday-week (a "session" = a day is not required; the goal is a per-week
        // session count, matching FrequencyDaysPerWeek's units).
        var sessionsByWeek = materialized
            .GroupBy(x => x.WeekStart)
            .ToDictionary(g => g.Key, g => g.Count());

        int? consistencyPct = null;
        var streak = 0;

        if (goal is int weeklyGoal && weeklyGoal > 0)
        {
            // D10: "weeks observed" = the Monday weeks from the FIRST completed session in the selected window
            // through the current week (so quiet weeks AFTER the user started count as misses, but the empty
            // weeks BEFORE their first session don't penalize a newcomer). Capped at the window by construction
            // (the earliest session is itself bounded to ≥ windowStart). Null when there are no sessions.
            var observationStart = sessionsByWeek.Count > 0
                ? sessionsByWeek.Keys.Min()
                : (DateOnly?)null;

            if (observationStart is DateOnly firstWeek)
            {
                var weekAnchors = new List<DateOnly>();
                for (var w = firstWeek; w <= currentWeekStart; w = w.AddDays(7))
                    weekAnchors.Add(w);

                var weeksHitting = weekAnchors.Count(w => sessionsByWeek.GetValueOrDefault(w) >= weeklyGoal);
                consistencyPct = (int)Math.Round(100m * weeksHitting / weekAnchors.Count, MidpointRounding.AwayFromZero);

                // Current streak = consecutive most-recent weeks hitting the goal, walking back from this week.
                for (var i = weekAnchors.Count - 1; i >= 0; i--)
                {
                    if (sessionsByWeek.GetValueOrDefault(weekAnchors[i]) >= weeklyGoal)
                        streak++;
                    else
                        break;
                }
            }
        }

        // Report the EFFECTIVE clamped window so the client can label the heatmap correctly.
        return new ConsistencyDto(windowWeeks, days, consistencyPct, streak);
    }

    private static IReadOnlyList<LiftDirectionDto> BuildTopLifts(
        IEnumerable<(DateOnly WeekStart, SessionRow Row)> rows)
    {
        // Flatten to one e1RM point per (lift, session) in the SHARED gathering shape, then reduce via the
        // SHARED StrengthLiftSeries (same code path the full strength-lift list uses) so the home strip and the
        // list agree by construction. The overview applies the ≥4-session honesty gate as a HARD filter (a thin
        // lift is omitted entirely) and caps at the top 3 by current e1RM.
        var points = rows
            .SelectMany(x => x.Row.Lifts.Select(l => new StrengthLiftSeries.LiftPoint(
                l.ExerciseId,
                l.ExerciseName,
                x.WeekStart,
                x.Row.StartedAt,
                l.BestE1rmKg!.Value)))
            .ToList();

        return StrengthLiftSeries.ToTrends(points)
            .Where(t => t.SessionCount >= MinSessionsForTopLift)   // < 4 sessions ⇒ direction is noise, omit
            .OrderByDescending(t => t.SessionCount)                // top-3 by frequency …
            .ThenBy(t => t.ExerciseId)                             // … deterministic tie-break
            .Take(MaxTopLifts)
            .Select(t => new LiftDirectionDto(
                t.ExerciseId,
                t.ExerciseName,
                t.Trend.CurrentE1rmKg,
                t.Trend.DeltaKgVsTrailing4w,
                t.Trend.Direction,
                t.Trend.Stalled,
                t.Trend.StallSessions,
                t.Spark))
            .ToList();
    }

    private async Task<IReadOnlyList<PersonalRecordDto>> ResolveRecentPrsAsync(CancellationToken cancellationToken)
    {
        var recordsResult = await mediator.Send(new GetMyPersonalRecordsQuery(), cancellationToken);
        if (recordsResult.IsFailure)
            return [];

        // GetMyPersonalRecordsQuery already returns the current best per lift, e1RM-sorted desc — take 3.
        return recordsResult.Value!.Records.Take(3).ToList();
    }

    // Shapes materialized from the single bounded read; named types so the EF projection + the in-memory
    // passes share one schema (and so the LINQ translates cleanly).
    private sealed class SessionRow
    {
        public DateTimeOffset StartedAt { get; init; }
        public string? ClientTimezone { get; init; }
        public Guid? TenantId { get; init; }
        public List<LiftRow> Lifts { get; init; } = [];
    }

    private sealed class LiftRow
    {
        public Guid ExerciseId { get; init; }
        public string? ExerciseName { get; init; }
        public decimal? BestE1rmKg { get; init; }
    }
}
