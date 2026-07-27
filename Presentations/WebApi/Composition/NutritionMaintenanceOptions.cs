namespace WebApi.Composition;

/// <summary>
/// Tunables for the nutrition day-close sweep (<see cref="NutritionStaleDayCloser"/>), bound from the
/// "NutritionMaintenance" config section.
///
/// <para>
/// Closing a stale day only finalizes adherence and marks still-planned items Missed; the
/// <c>DailyLogClosedEvent</c> it raises has no server-side consumer, so a late close has no effect beyond how
/// a past day is displayed the next time it is viewed. The cadence is therefore purely a
/// finalization-latency vs. database-idle trade-off: a longer <see cref="Interval"/> means fewer periodic
/// queries (so a serverless Postgres can stay suspended longer) at the cost of a day showing "open" for a
/// little longer past its local midnight.
/// </para>
/// </summary>
public sealed class NutritionMaintenanceOptions
{
    public const string SectionName = "NutritionMaintenance";

    /// <summary>Master switch for the background day-close sweep.</summary>
    public bool DayCloseEnabled { get; set; } = true;

    /// <summary>Delay before the first sweep, so it does not contend with startup migrations/seeding.</summary>
    public TimeSpan DayCloseInitialDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>How often stale days are swept for closing. Default 6h (4 sweeps/day) rather than the old
    /// 15 minutes (96/day) — the day-close has no time-critical consumer, so it need not poll frequently.</summary>
    public TimeSpan DayCloseInterval { get; set; } = TimeSpan.FromHours(6);
}
