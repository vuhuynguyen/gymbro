using BuildingBlocks.Shared.Time;
using BuildingBlocks.Shared.Tracking;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application;

/// <summary>
/// The single source of the Progress strength windowed-e1RM GATHERING — the read that turns a self-scoped
/// session query into one MAX-qualifying-working-set e1RM point per (lift, session) inside a Monday-anchored
/// window, plus the per-lift reduction to a <see cref="E1rmSeriesCalculator.Trend"/> + spark. Extracted from
/// <c>GetMyProgressOverviewHandler</c> so the overview's top-3 strip and the full strength-lift list
/// (<c>GetMyStrengthLiftsHandler</c>) gather identically — only the cap differs. The honesty gate
/// (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight) lives in the SQL projection here; the
/// direction/stall/delta math stays in the shared <see cref="E1rmSeriesCalculator"/>.
/// </summary>
public static class StrengthLiftSeries
{
    /// <summary>Below this many qualifying sessions a lift's direction is noise — callers decide what to do
    /// (the overview omits the lift; the full list keeps it but flips the honesty flag off).</summary>
    public const int MinSessionsForTrend = 4;

    /// <summary>One e1RM point for a lift in one session: the session's local Monday week (for the trailing
    /// baseline + window bucketing), its start instant (chronological ordering), and its MAX qualifying
    /// working-set e1RM.</summary>
    public readonly record struct LiftPoint(
        Guid ExerciseId, string? ExerciseName, DateOnly WeekStart, DateTimeOffset StartedAt, decimal E1rmKg);

    /// <summary>A lift's full gathered trend: the shared calculator's <see cref="E1rmSeriesCalculator.Trend"/>,
    /// its spark series, the qualifying session count, and a stable display name.</summary>
    public readonly record struct LiftTrend(
        Guid ExerciseId,
        string? ExerciseName,
        int SessionCount,
        E1rmSeriesCalculator.Trend Trend,
        IReadOnlyList<decimal> Spark);

    /// <summary>
    /// One bounded, self-scoped read → one MAX-qualifying-working-set e1RM point per (lift, session) inside the
    /// window. <paramref name="sessions"/> MUST already be the caller's own sessions (e.g.
    /// <c>QueryOwnAcrossGyms</c>); this method adds only the completed-only + window + honesty-gate filters and
    /// the MAX-per-session reduction. The conservative one-day UTC slack and the exact local-week re-bucketing
    /// mirror the overview so the two surfaces agree by construction.
    /// </summary>
    public static async Task<IReadOnlyList<LiftPoint>> GatherAsync(
        IQueryable<WorkoutSession> sessions,
        DateOnly windowStart,
        DateOnly currentWeekStart,
        string? userZone,
        CancellationToken cancellationToken)
    {
        var windowLowerBoundUtc = LocalDayResolver
            .StartOfLocalDayUtc(windowStart, userZone)
            .AddDays(-1);

        // Honesty gate applied server-side: only Working sets with an e1RM, reps ≤ 12, on a strength/bodyweight
        // lift contribute. MAX e1RM over the qualifying working sets = one point per (lift, session); drop/
        // AMRAP/failure stages carry null e1RM, so MAX is cluster-safe (no ParentSetId filter).
        var raw = await sessions
            .Where(s => s.Status == SessionStatus.Completed && s.StartedAt >= windowLowerBoundUtc)
            .Select(s => new
            {
                s.StartedAt,
                s.ClientTimezone,
                Lifts = s.Exercises
                    .Where(e => e.TrackingType == ExerciseTrackingType.Strength
                        || e.TrackingType == ExerciseTrackingType.Bodyweight)
                    .Select(e => new
                    {
                        e.ExerciseId,
                        e.ExerciseName,
                        BestE1rmKg = e.Sets
                            .Where(set => set.SetType == PerformedSetType.Working
                                && set.EstimatedOneRepMaxKg != null
                                && set.Reps != null && set.Reps <= SessionPrRules.MaxPrReps)
                            .Max(set => (decimal?)set.EstimatedOneRepMaxKg)
                    })
            })
            .ToListAsync(cancellationToken);

        // Re-bucket each session to its own local Monday week, drop anything outside the window, and drop lift
        // points whose session had no qualifying working set (null e1RM).
        var points = new List<LiftPoint>();
        foreach (var s in raw)
        {
            var weekStart = LocalDayResolver.WeekStartOf(s.StartedAt, s.ClientTimezone ?? userZone);
            if (weekStart < windowStart || weekStart > currentWeekStart)
                continue;

            foreach (var l in s.Lifts)
            {
                if (l.BestE1rmKg is not decimal e1rm)
                    continue;

                points.Add(new LiftPoint(l.ExerciseId, l.ExerciseName, weekStart, s.StartedAt, e1rm));
            }
        }

        return points;
    }

    /// <summary>
    /// Reduces gathered <see cref="LiftPoint"/>s to one <see cref="LiftTrend"/> per lift via the shared
    /// <see cref="E1rmSeriesCalculator"/>. Each lift's points are ordered oldest → newest before the
    /// calculator runs. The result order is deterministic (exercise id) but NOT meaningful — callers apply
    /// their own session-count gate, sort, and cap (the overview ranks by frequency; the full list by e1RM).
    /// </summary>
    public static IReadOnlyList<LiftTrend> ToTrends(IReadOnlyList<LiftPoint> points)
    {
        return points
            .GroupBy(p => p.ExerciseId)
            .Select(g =>
            {
                var series = g.OrderBy(p => p.StartedAt).ToList();
                var calcPoints = series
                    .Select((p, i) => new E1rmSeriesCalculator.Point(p.WeekStart, i, p.E1rmKg))
                    .ToList();

                return new LiftTrend(
                    g.Key,
                    series[^1].ExerciseName,
                    series.Count,
                    E1rmSeriesCalculator.Compute(calcPoints),
                    E1rmSeriesCalculator.Spark(calcPoints));
            })
            .OrderBy(t => t.ExerciseId)
            .ToList();
    }
}
