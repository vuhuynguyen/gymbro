using Modules.WorkoutSessionModule.Application;
using Modules.WorkoutSessionModule.Application.DTOs;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The shared strength math extracted from the overview handler (used by BOTH the overview and the per-lift
/// e1rm-series drill-down). Pure, no database. Pins Direction (±0.5 kg vs the trailing-4-week mean), the
/// 3-exposure stall, the order-independence (points are re-sorted by week then ordinal), and the bounded
/// spark. Dates are relative anchors — no literal calendar date, so there is no time-bomb.
/// </summary>
public sealed class E1rmSeriesCalculatorTests
{
    // A fixed Monday anchor that has no calendar meaning to the math (only relative week gaps matter).
    private static readonly DateOnly W0 = MondayOf(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime));

    private static DateOnly MondayOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    // Week `weeksAgo` before W0 (0 = the latest week).
    private static DateOnly Week(int weeksAgo) => W0.AddDays(-7 * weeksAgo);

    private static E1rmSeriesCalculator.Point P(int weeksAgo, decimal e1rm, int ordinal = 0)
        => new(Week(weeksAgo), ordinal, e1rm);

    [Fact]
    public void Empty_series_is_flat_and_zeroed()
    {
        var trend = E1rmSeriesCalculator.Compute([]);

        Assert.Equal(0m, trend.CurrentE1rmKg);
        Assert.Equal(0m, trend.DeltaKgVsTrailing4w);
        Assert.Equal(LiftTrendDirection.Flat, trend.Direction);
        Assert.False(trend.Stalled);
        Assert.Equal(0, trend.StallSessions);
    }

    [Fact]
    public void Direction_is_up_when_current_clears_the_trailing_mean_by_more_than_half_a_kilo()
    {
        // Trailing four weeks at 100, current week 120 → delta +20 → Up.
        var trend = E1rmSeriesCalculator.Compute(
        [
            P(4, 100m), P(3, 100m), P(2, 100m), P(1, 100m), P(0, 120m)
        ]);

        Assert.Equal(120m, trend.CurrentE1rmKg);
        Assert.Equal(20m, trend.DeltaKgVsTrailing4w);
        Assert.Equal(LiftTrendDirection.Up, trend.Direction);
    }

    [Fact]
    public void Direction_is_down_when_current_falls_below_the_trailing_mean()
    {
        var trend = E1rmSeriesCalculator.Compute(
        [
            P(4, 120m), P(3, 120m), P(2, 120m), P(1, 120m), P(0, 100m)
        ]);

        Assert.Equal(LiftTrendDirection.Down, trend.Direction);
        Assert.True(trend.DeltaKgVsTrailing4w < -0.5m);
    }

    [Fact]
    public void Direction_is_flat_within_the_half_kilo_band()
    {
        var trend = E1rmSeriesCalculator.Compute(
        [
            P(4, 100m), P(3, 100m), P(2, 100m), P(1, 100m), P(0, 100.3m)
        ]);

        Assert.Equal(LiftTrendDirection.Flat, trend.Direction);
    }

    [Fact]
    public void Stall_flags_when_no_new_best_in_the_last_three_exposures()
    {
        // Best at week 4 (140), then three lower exposures → stalled, 3 since the best.
        var trend = E1rmSeriesCalculator.Compute(
        [
            P(4, 140m), P(3, 130m), P(2, 130m), P(1, 130m)
        ]);

        Assert.True(trend.Stalled);
        Assert.Equal(3, trend.StallSessions);
    }

    [Fact]
    public void A_fresh_best_on_the_latest_exposure_is_not_stalled()
    {
        var trend = E1rmSeriesCalculator.Compute(
        [
            P(3, 100m), P(2, 110m), P(1, 120m), P(0, 130m)
        ]);

        Assert.False(trend.Stalled);
        Assert.Equal(0, trend.StallSessions);
    }

    [Fact]
    public void Points_are_reordered_internally_so_input_order_does_not_matter()
    {
        var ascending = new[] { P(4, 100m), P(3, 100m), P(2, 100m), P(1, 100m), P(0, 120m) };
        var shuffled = new[] { P(0, 120m), P(2, 100m), P(4, 100m), P(1, 100m), P(3, 100m) };

        var fromAscending = E1rmSeriesCalculator.Compute(ascending);
        var fromShuffled = E1rmSeriesCalculator.Compute(shuffled);

        Assert.Equal(fromAscending, fromShuffled);
        Assert.Equal(120m, fromShuffled.CurrentE1rmKg);   // newest week wins regardless of input order
    }

    [Fact]
    public void Spark_returns_the_most_recent_points_oldest_to_newest_bounded()
    {
        var points = Enumerable.Range(0, 12)
            .Select(i => P(11 - i, 100m + i))   // week-11 = 100 … week-0 = 111
            .ToList();

        var spark = E1rmSeriesCalculator.Spark(points, maxPoints: 8);

        Assert.Equal(8, spark.Count);
        Assert.Equal(104m, spark[0]);    // the 8 most recent, oldest → newest
        Assert.Equal(111m, spark[^1]);
    }
}
