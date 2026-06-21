using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
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
/// e1RM directions, and a PR teaser (the top 3 PRs whose best was SET within the selected window, from
/// <see cref="GetMyPersonalRecordsQuery"/>). The This-Week hero,
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

    // ── v2 window differentiation (WINDOW-DIFFERENTIATION.md) ──
    // Self-scoped acute/chronic LOAD — mirrors GetClientLoadHandler exactly (volume-only, SOFT band, NEVER an
    // exposed ACWR ratio; FEASIBILITY R10). Acute = last 7 days; chronic = last 28 days ÷ 4 = avg weekly load.
    private const int AcuteDays = 7;
    private const int ChronicDays = 28;
    private const decimal RampingFactor = 1.5m;
    private const decimal DetrainingFactor = 0.8m;
    // ≤ 6 weeks reads as a "block" (execution/momentum, vs last block); longer is a "phase" (adaptation/strategy).
    private const int BlockWindowMaxWeeks = 6;
    // Under this many hard sets/week a muscle is flagged light (client renders the soft 10–20 growth zone).
    private const decimal UnderDosedSetsPerWeek = 10m;
    private const int PlateauFlagWeeks = 4;   // a lift with no new best for ≥ this many weeks is a phase plateau

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

        // One MAX-qualifying-working-set e1RM point per (lift, session) drives BOTH the top-lift direction strip
        // and the window strength-GAIN (first→last), so gather the points once.
        var liftPoints = windowRows
            .SelectMany(x => x.Row.Lifts.Select(l => new StrengthLiftSeries.LiftPoint(
                l.ExerciseId, l.ExerciseName, x.WeekStart, x.Row.StartedAt, l.BestE1rmKg!.Value)))
            .ToList();

        var topLifts = BuildTopLifts(liftPoints);
        var strengthGain = BuildStrengthGain(liftPoints, currentWeekStart);

        // In-window PRs feed BOTH the top-3 teaser and the TRUE count (PeriodStats.PrCount; the teaser caps at 3).
        var inWindowPrs = await ResolveInWindowPrsAsync(
            LocalDayResolver.StartOfLocalDayUtc(windowStart, userZone), cancellationToken);
        var recentPrs = inWindowPrs.Take(3).ToList();

        // v2: a second bounded read (volume / hard-sets / per-muscle) over the CURRENT + PREVIOUS window, so the
        // period deltas, weekly slope, per-muscle dosing and 7/28-day load all come from one materialization.
        var prevWindowStart = windowStart.AddDays(-7 * windowWeeks);
        var statRows = await GatherStatRowsAsync(prevWindowStart, userZone, cancellationToken);

        var period = BuildPeriodStats(
            statRows, windowStart, prevWindowStart, currentWeekStart, inWindowPrs.Count);
        var muscleVolume = await BuildMuscleVolumeAsync(
            statRows, windowStart, prevWindowStart, currentWeekStart, windowWeeks, cancellationToken);
        var load = BuildLoadBalance(statRows, now);
        var coach = BuildCoachRead(
            isBlock: windowWeeks <= BlockWindowMaxWeeks,
            thisWeek, consistency, topLifts, strengthGain, period, muscleVolume, load);

        return Result<ProgressOverviewDto>.Success(new ProgressOverviewDto(
            thisWeek,
            consistency,
            topLifts,
            recentPrs,
            GeneratedAtUtc: now,
            Period: period,
            StrengthGain: strengthGain,
            MuscleVolume: muscleVolume,
            Load: load,
            Coach: coach));
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
        IReadOnlyList<StrengthLiftSeries.LiftPoint> points)
    {
        // Reduce the gathered points via the SHARED StrengthLiftSeries (the same code path the full strength-lift
        // list uses) so the home strip and the list agree by construction. The overview applies the ≥4-session
        // honesty gate as a HARD filter (a thin lift is omitted entirely) and caps at the top 3 by frequency.
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

    private async Task<IReadOnlyList<PersonalRecordDto>> ResolveInWindowPrsAsync(
        DateTimeOffset windowFromUtc,
        CancellationToken cancellationToken)
    {
        var recordsResult = await mediator.Send(new GetMyPersonalRecordsQuery(), cancellationToken);
        if (recordsResult.IsFailure)
            return [];

        // GetMyPersonalRecordsQuery returns the current best per lift, e1RM-sorted desc. Keep only PRs actually
        // SET within the selected window (AchievedAt ≥ windowFromUtc). The caller takes the top 3 for the teaser
        // and the COUNT for PeriodStats.PrCount (a lift whose all-time best predates the window contributes none).
        return recordsResult.Value!.Records
            .Where(r => r.AchievedAt >= windowFromUtc)
            .ToList();
    }

    // ── v2 window differentiation: period stats · strength gain · muscle dosing · load · coach's-read ──

    private async Task<List<StatRow>> GatherStatRowsAsync(
        DateOnly fromWeekStart, string? userZone, CancellationToken cancellationToken)
    {
        // A SECOND bounded self-scoped read (volume / hard-sets / per-muscle), spanning the CURRENT + PREVIOUS
        // window. Conservative one-day UTC slack mirrors the strength read; exact week bucketing is in memory.
        var lowerBoundUtc = LocalDayResolver.StartOfLocalDayUtc(fromWeekStart, userZone).AddDays(-1);

        var raw = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .Where(s => s.Status == SessionStatus.Completed && s.StartedAt >= lowerBoundUtc)
            .Select(s => new
            {
                s.StartedAt,
                s.ClientTimezone,
                // Σ weight×reps over non-warmup sets carrying both values (drop/AMRAP stages count) — parity with
                // SessionMapping.ComputeVolumeKg / GetMyProgressHandler / GetClientLoadHandler.
                VolumeKg = s.Exercises.SelectMany(e => e.Sets)
                    .Where(set => set.SetType != PerformedSetType.Warmup
                        && set.WeightKg != null && set.Reps != null)
                    .Sum(set => (decimal?)(set.WeightKg!.Value * set.Reps!.Value)) ?? 0m,
                // Hard sets = Working LEAD sets (a drop cluster counts once → ParentSetId IS NULL).
                WorkingSets = s.Exercises.SelectMany(e => e.Sets)
                    .Count(set => set.SetType == PerformedSetType.Working && set.ParentSetId == null),
                Muscles = s.Exercises
                    .Select(e => new
                    {
                        e.ExerciseId,
                        Sets = e.Sets.Count(set =>
                            set.SetType == PerformedSetType.Working && set.ParentSetId == null)
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return raw
            .Select(s => new StatRow
            {
                WeekStart = LocalDayResolver.WeekStartOf(s.StartedAt, s.ClientTimezone ?? userZone),
                StartedAt = s.StartedAt,
                VolumeKg = s.VolumeKg,
                WorkingSets = s.WorkingSets,
                Muscles = s.Muscles
                    .Where(m => m.Sets > 0)
                    .Select(m => new MuscleSet(m.ExerciseId, m.Sets))
                    .ToList()
            })
            .ToList();
    }

    private static PeriodStatsDto BuildPeriodStats(
        IReadOnlyList<StatRow> rows, DateOnly windowStart, DateOnly prevWindowStart,
        DateOnly currentWeekStart, int prCount)
    {
        var prevWindowEnd = windowStart.AddDays(-7);
        var cur = rows.Where(r => r.WeekStart >= windowStart && r.WeekStart <= currentWeekStart).ToList();
        var prev = rows.Where(r => r.WeekStart >= prevWindowStart && r.WeekStart <= prevWindowEnd).ToList();

        // Dense weekly volume — one entry per window week (0 for quiet weeks), oldest→newest — for the slope.
        var weekly = new List<decimal>();
        for (var w = windowStart; w <= currentWeekStart; w = w.AddDays(7))
            weekly.Add(Round1(cur.Where(r => r.WeekStart == w).Sum(r => r.VolumeKg)));

        return new PeriodStatsDto(
            cur.Count, prev.Count,
            Round1(cur.Sum(r => r.VolumeKg)), Round1(prev.Sum(r => r.VolumeKg)),
            cur.Sum(r => r.WorkingSets), prev.Sum(r => r.WorkingSets),
            prCount, weekly);
    }

    private static StrengthGainDto BuildStrengthGain(
        IReadOnlyList<StrengthLiftSeries.LiftPoint> points, DateOnly currentWeekStart)
    {
        var lifts = points
            .GroupBy(p => p.ExerciseId)
            .Select(g => g.OrderBy(p => p.StartedAt).ToList())
            .Where(series => series.Count >= MinSessionsForTopLift)   // same honesty gate as the top-lift strip
            .Select(series =>
            {
                var start = series[0].E1rmKg;
                var current = series[^1].E1rmKg;
                var gain = current - start;
                // PlateauWeeks = weeks since the window's best e1RM (0 while still climbing). On a tie the LATER
                // session wins, so a re-hit best reads as freshly set.
                var best = series.Aggregate((a, b) => b.E1rmKg >= a.E1rmKg ? b : a);
                var plateau = Math.Max(0, (currentWeekStart.DayNumber - best.WeekStart.DayNumber) / 7);
                return new LiftGainDto(
                    series[0].ExerciseId,
                    series[^1].ExerciseName,
                    Round1(start), Round1(current), Round1(gain),
                    start > 0m ? Round1(gain / start * 100m) : 0m,
                    plateau);
            })
            .OrderByDescending(l => l.CurrentE1rmKg)
            .ThenBy(l => l.ExerciseId)
            .ToList();

        var avg = lifts.Count > 0 ? Round1(lifts.Average(l => l.GainPct)) : 0m;
        return new StrengthGainDto(avg, lifts);
    }

    private async Task<IReadOnlyList<MuscleVolumeDto>> BuildMuscleVolumeAsync(
        IReadOnlyList<StatRow> rows, DateOnly windowStart, DateOnly prevWindowStart,
        DateOnly currentWeekStart, int windowWeeks, CancellationToken cancellationToken)
    {
        var prevWindowEnd = windowStart.AddDays(-7);
        var cur = rows.Where(r => r.WeekStart >= windowStart && r.WeekStart <= currentWeekStart).ToList();
        var prev = rows.Where(r => r.WeekStart >= prevWindowStart && r.WeekStart <= prevWindowEnd).ToList();

        var exerciseIds = cur.Concat(prev)
            .SelectMany(r => r.Muscles)
            .Select(m => m.ExerciseId)
            .Distinct()
            .ToList();
        if (exerciseIds.Count == 0)
            return [];

        // Cross-module: resolve each lift's PRIMARY muscle group (same resolver the strength-lift list uses).
        var muscleResult = await mediator.Send(
            new ResolveExerciseMuscleGroupsQuery(exerciseIds), cancellationToken);
        var map = muscleResult.IsSuccess
            ? muscleResult.Value!
            : new Dictionary<Guid, string>();
        if (map.Count == 0)
            return [];

        decimal weeks = windowWeeks <= 0 ? 1 : windowWeeks;
        var curTally = TallyMuscles(cur, map);
        var prevTally = TallyMuscles(prev, map);

        return curTally.Keys.Union(prevTally.Keys)
            .Select(g => new MuscleVolumeDto(
                g,
                Round1(curTally.GetValueOrDefault(g) / weeks),
                Round1(prevTally.GetValueOrDefault(g) / weeks)))
            .OrderByDescending(m => m.SetsPerWeek)
            .ThenBy(m => m.Muscle)
            .ToList();
    }

    private static Dictionary<string, int> TallyMuscles(
        IEnumerable<StatRow> rows, IReadOnlyDictionary<Guid, string> map)
    {
        var tally = new Dictionary<string, int>();
        foreach (var r in rows)
            foreach (var m in r.Muscles)
                if (map.TryGetValue(m.ExerciseId, out var group) && !string.IsNullOrEmpty(group))
                    tally[group] = tally.GetValueOrDefault(group) + m.Sets;
        return tally;
    }

    private static LoadBalanceDto BuildLoadBalance(IReadOnlyList<StatRow> rows, DateTimeOffset now)
    {
        var acuteFrom = now.AddDays(-AcuteDays);
        var chronicFrom = now.AddDays(-ChronicDays);
        var chronicTotal = rows.Where(r => r.StartedAt >= chronicFrom).Sum(r => r.VolumeKg);
        var acuteVolume = rows.Where(r => r.StartedAt >= acuteFrom).Sum(r => r.VolumeKg);
        var chronicWeekly = chronicTotal / (ChronicDays / 7);   // 28 ÷ 7 = 4 → average weekly load
        return new LoadBalanceDto(
            Round1(acuteVolume), Round1(chronicWeekly), ClassifyLoad(acuteVolume, chronicWeekly));
    }

    // SOFT band over acute vs the chronic weekly average — a gentle nudge, never an exposed ACWR ratio (R10).
    private static LoadTrend ClassifyLoad(decimal acuteVolume, decimal chronicWeekly)
    {
        if (chronicWeekly <= 0m)
            return acuteVolume > 0m ? LoadTrend.Ramping : LoadTrend.Steady;
        if (acuteVolume > chronicWeekly * RampingFactor)
            return LoadTrend.Ramping;
        if (acuteVolume < chronicWeekly * DetrainingFactor)
            return LoadTrend.Detraining;
        return LoadTrend.Steady;
    }

    // The rule-based "coach's read" — a WRITER over the already-computed aggregates, never a new number. The short
    // window frames a BLOCK (execution/momentum, vs last block); the long window a PHASE (adaptation/strategy).
    private static CoachReadDto BuildCoachRead(
        bool isBlock,
        WeekAdherenceDto thisWeek,
        ConsistencyDto consistency,
        IReadOnlyList<LiftDirectionDto> topLifts,
        StrengthGainDto strengthGain,
        PeriodStatsDto period,
        IReadOnlyList<MuscleVolumeDto> muscleVolume,
        LoadBalanceDto load)
    {
        // Honest empty state — never a fabricated verdict.
        if (period.Sessions == 0)
            return new CoachReadDto(
                "Not enough logged yet",
                "Train a few sessions and your trend will appear here.",
                null,
                CoachTone.Neutral);

        var upLifts = topLifts.Count(l => l.Direction == LiftTrendDirection.Up);
        var downLifts = topLifts.Count(l => l.Direction == LiftTrendDirection.Down);
        var stalled = topLifts.FirstOrDefault(l => l.Stalled);
        var underDosed = muscleVolume
            .Where(m => m.SetsPerWeek > 0m && m.SetsPerWeek < UnderDosedSetsPerWeek)
            .OrderBy(m => m.SetsPerWeek)
            .FirstOrDefault();

        if (isBlock)
        {
            var volPiece = period.PrevVolumeKg > 0m
                ? (period.VolumeKg >= period.PrevVolumeKg
                    ? $"volume up {Pct(period.VolumeKg - period.PrevVolumeKg, period.PrevVolumeKg)}% on the last block"
                    : $"volume down {Pct(period.PrevVolumeKg - period.VolumeKg, period.PrevVolumeKg)}% on the last block")
                : "volume building";
            var strengthPiece = upLifts > 0
                ? $"{upLifts} lift{(upLifts == 1 ? "" : "s")} trending up"
                : downLifts > 0
                    ? $"{downLifts} lift{(downLifts == 1 ? "" : "s")} slipping"
                    : "strength holding";

            var tone = downLifts > upLifts || load.Trend == LoadTrend.Ramping
                ? CoachTone.Watch
                : upLifts > 0 ? CoachTone.Positive : CoachTone.Neutral;
            var headline = tone == CoachTone.Positive
                ? "Momentum's with you"
                : tone == CoachTone.Watch ? "Mixed block — worth a look" : "Steady block";

            string? action =
                stalled is not null
                    ? $"{stalled.ExerciseName ?? "A main lift"} has stalled — deload or change the rep scheme."
                : load.Trend == LoadTrend.Ramping
                    ? "Volume is ramping fast — keep an eye on recovery this week."
                : thisWeek.Goal is int goal && thisWeek.CompletedSessions < goal
                    ? $"{goal - thisWeek.CompletedSessions} more session{(goal - thisWeek.CompletedSessions == 1 ? "" : "s")} to hit this week's goal."
                : underDosed is not null
                    ? $"{Cap(underDosed.Muscle)} is light this block — add a set."
                : null;

            return new CoachReadDto(headline, $"{Cap(strengthPiece)}, {volPiece}.", action, tone);
        }

        // Phase view — adaptation + program effectiveness.
        var gain = strengthGain.AvgGainPct;
        var volTrend = WeeklyTrend(period.WeeklyVolumeKg);
        var plateau = strengthGain.Lifts
            .Where(l => l.PlateauWeeks >= PlateauFlagWeeks)
            .OrderByDescending(l => l.PlateauWeeks)
            .FirstOrDefault();

        var phaseTone = gain > 1m && volTrend != "falling"
            ? CoachTone.Positive
            : gain <= 0m ? CoachTone.Watch : CoachTone.Neutral;
        var phaseHeadline = phaseTone == CoachTone.Positive
            ? "Your program is working"
            : phaseTone == CoachTone.Watch ? "Progress has stalled this phase" : "Holding steady";

        var gainPiece = gain > 0m
            ? $"estimated strength up {gain:0.#}% across your main lifts"
            : gain < 0m ? $"estimated strength down {-gain:0.#}%" : "estimated strength flat";
        var volTrendPiece = volTrend == "rising" ? "volume trending up"
            : volTrend == "falling" ? "volume trending down" : "volume steady";

        string? phaseAction =
            plateau is not null
                ? $"{plateau.ExerciseName ?? "A main lift"} hasn't progressed in {plateau.PlateauWeeks} weeks — change the stimulus next phase."
            : underDosed is not null
                ? $"{Cap(underDosed.Muscle)} has been under-dosed all phase — add 1–2 sets a week."
            : volTrend == "falling"
                ? "Weekly volume is drifting down — add work or a fresh progression next phase."
            : null;

        return new CoachReadDto(
            phaseHeadline,
            $"{Cap(gainPiece)}, {volTrendPiece} over {period.WeeklyVolumeKg.Count} weeks.",
            phaseAction,
            phaseTone);
    }

    // Coarse rising/flat/falling slope: the last third of the weekly-volume series vs the first third.
    private static string WeeklyTrend(IReadOnlyList<decimal> weekly)
    {
        if (weekly.Count < 4)
            return "flat";
        var third = Math.Max(1, weekly.Count / 3);
        var first = weekly.Take(third).Average();
        var last = weekly.Skip(weekly.Count - third).Average();
        if (first <= 0m)
            return last > 0m ? "rising" : "flat";
        var change = (last - first) / first;
        if (change > 0.1m) return "rising";
        if (change < -0.1m) return "falling";
        return "flat";
    }

    private static decimal Round1(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

    private static int Pct(decimal part, decimal whole) =>
        whole <= 0m ? 0 : (int)Math.Round(part / whole * 100m, MidpointRounding.AwayFromZero);

    private static string Cap(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

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

    // v2 stats read: one row per completed session (volume / hard-set count / per-exercise hard-set counts).
    private sealed class StatRow
    {
        public DateOnly WeekStart { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public decimal VolumeKg { get; init; }
        public int WorkingSets { get; init; }
        public List<MuscleSet> Muscles { get; init; } = [];
    }

    private readonly record struct MuscleSet(Guid ExerciseId, int Sets);
}
