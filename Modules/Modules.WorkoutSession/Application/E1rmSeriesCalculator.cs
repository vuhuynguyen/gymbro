using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application;

/// <summary>
/// The single source of the Progress-page strength math: given one lift's per-session-best e1RM series
/// (oldest → newest, one point per session), it derives the current e1RM, the delta versus the trailing
/// 4-week baseline, the trend <see cref="LiftTrendDirection"/>, the stall flag, and a bounded spark series.
/// Extracted from <c>GetMyProgressOverviewHandler</c> so the overview and the per-lift drill-down
/// (<c>GetMyExerciseE1rmSeriesQuery</c>) compute strength direction/stall IDENTICALLY — pure, allocation-light,
/// and unit-testable without a database. Callers own the honesty gate (Working ∧ e1RM ∧ Reps ≤ 12 ∧
/// Strength/Bodyweight) and the MAX-per-session reduction; this type only consumes the already-gated series.
/// </summary>
public static class E1rmSeriesCalculator
{
    public const int TrailingWeeks = 4;        // baseline window for strength direction
    public const int StallExposureWindow = 3;  // no new best in the last K exposures ⇒ stalled
    public const decimal DirectionThresholdKg = 0.5m;
    public const int DefaultSparkPoints = 8;

    /// <summary>One session-best e1RM point for a lift: the session's local Monday week (for the trailing
    /// baseline) and its MAX qualifying working-set e1RM. Series order is the caller's responsibility — the
    /// calculator orders by <see cref="WeekStart"/> then <see cref="Ordinal"/> so it never depends on the
    /// input ordering.</summary>
    public readonly record struct Point(DateOnly WeekStart, int Ordinal, decimal E1rmKg);

    /// <summary>The derived strength trend for one lift over its session-best series.</summary>
    public readonly record struct Trend(
        decimal CurrentE1rmKg,
        decimal DeltaKgVsTrailing4w,
        LiftTrendDirection Direction,
        bool Stalled,
        int StallSessions);

    /// <summary>
    /// Derives the trend for one lift from its session-best e1RM points. Points are re-ordered internally
    /// (week asc, then ordinal asc), so callers may pass them in any order. An empty series yields a
    /// zeroed/flat trend — callers decide whether to surface the lift at all.
    /// </summary>
    public static Trend Compute(IReadOnlyList<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
            return new Trend(0m, 0m, LiftTrendDirection.Flat, Stalled: false, StallSessions: 0);

        var ordered = points
            .OrderBy(p => p.WeekStart)
            .ThenBy(p => p.Ordinal)
            .ToList();

        var current = ordered[^1];
        var currentE1rm = current.E1rmKg;

        // Trailing baseline = mean of session-bests in the 4 weeks PRIOR to the current point's week.
        var trailingStart = current.WeekStart.AddDays(-7 * TrailingWeeks);
        var trailing = ordered
            .Where(p => p.WeekStart >= trailingStart && p.WeekStart < current.WeekStart)
            .Select(p => p.E1rmKg)
            .ToList();

        var deltaKg = trailing.Count > 0
            ? Math.Round(currentE1rm - (trailing.Sum() / trailing.Count), 1)
            : 0m;

        var direction = deltaKg > DirectionThresholdKg
            ? LiftTrendDirection.Up
            : deltaKg < -DirectionThresholdKg
                ? LiftTrendDirection.Down
                : LiftTrendDirection.Flat;

        var (stalled, stallSessions) = ComputeStall(ordered.Select(p => p.E1rmKg).ToList());

        return new Trend(currentE1rm, deltaKg, direction, stalled, stallSessions);
    }

    /// <summary>Up to <paramref name="maxPoints"/> most-recent session-best e1RM values, oldest → newest.</summary>
    public static IReadOnlyList<decimal> Spark(IReadOnlyList<Point> points, int maxPoints = DefaultSparkPoints)
    {
        ArgumentNullException.ThrowIfNull(points);
        return points
            .OrderBy(p => p.WeekStart)
            .ThenBy(p => p.Ordinal)
            .Select(p => p.E1rmKg)
            .TakeLast(maxPoints)
            .ToList();
    }

    // Stalled when the running-best e1RM was last exceeded more than K exposures ago. StallSessions = the
    // number of exposures since that last new best (0 when the most recent exposure set a new best).
    private static (bool Stalled, int StallSessions) ComputeStall(IReadOnlyList<decimal> e1rmSeries)
    {
        var best = decimal.MinValue;
        var lastNewBestIndex = -1;
        for (var i = 0; i < e1rmSeries.Count; i++)
        {
            if (e1rmSeries[i] > best)
            {
                best = e1rmSeries[i];
                lastNewBestIndex = i;
            }
        }

        var exposuresSinceBest = e1rmSeries.Count - 1 - lastNewBestIndex;
        var stalled = exposuresSinceBest >= StallExposureWindow;
        return (stalled, stalled ? exposuresSinceBest : 0);
    }
}
