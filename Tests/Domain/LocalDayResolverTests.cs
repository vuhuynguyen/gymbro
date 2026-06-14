using BuildingBlocks.Shared.Time;
using Xunit;

namespace Gymbro.Tests.Domain;

/// <summary>
/// The day-boundary math behind every timezone-aware read (history/nutrition "today", week bucketing). A single
/// instant lands on different calendar days by zone, and a local calendar day maps to a UTC half-open instant
/// range — getting either wrong silently excludes a trainee's late-evening session or leaks the next day's.
/// </summary>
public sealed class LocalDayResolverTests
{
    // ── LocalDateOf: one instant → the local calendar day in the given zone ──

    [Fact]
    public void Local_date_of_an_instant_differs_by_zone()
    {
        // 2026-01-08 02:00 UTC: already the 8th in Bangkok (+07), still the 7th in Toronto (−05).
        var instant = new DateTimeOffset(2026, 1, 8, 2, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 8), LocalDayResolver.LocalDateOf(instant, "Asia/Bangkok"));
        Assert.Equal(new DateOnly(2026, 1, 7), LocalDayResolver.LocalDateOf(instant, "America/Toronto"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    public void Local_date_falls_back_to_utc_for_an_absent_or_unknown_zone(string? zone)
    {
        var instant = new DateTimeOffset(2026, 1, 8, 2, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 1, 8), LocalDayResolver.LocalDateOf(instant, zone));
    }

    // ── StartOfLocalDayUtc: a local calendar day → the UTC instant of its midnight ──

    [Fact]
    public void Start_of_local_day_converts_midnight_in_zone_to_utc()
    {
        var day = new DateOnly(2026, 1, 1);

        // Toronto midnight (EST, −05) is 05:00 UTC; Bangkok midnight (+07) is the prior day 17:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.Zero),
            LocalDayResolver.StartOfLocalDayUtc(day, "America/Toronto"));
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 17, 0, 0, TimeSpan.Zero),
            LocalDayResolver.StartOfLocalDayUtc(day, "Asia/Bangkok"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Not/AZone")]
    public void Start_of_local_day_falls_back_to_utc_midnight_for_an_absent_or_unknown_zone(string? zone)
        => Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LocalDayResolver.StartOfLocalDayUtc(new DateOnly(2026, 1, 1), zone));

    // ── The half-open [from, toExclusive) window the history/nutrition handlers build ──

    [Fact]
    public void Late_evening_session_west_of_utc_stays_inside_its_own_local_day()
    {
        const string zone = "America/Toronto";
        var today = new DateOnly(2026, 1, 15);

        var from = LocalDayResolver.StartOfLocalDayUtc(today, zone);
        var toExclusive = LocalDayResolver.StartOfLocalDayUtc(today.AddDays(1), zone);

        // Logged 23:30 Toronto on the 15th = 04:30 UTC on the 16th — would be dropped by a naive UTC day window.
        var session = new DateTimeOffset(2026, 1, 16, 4, 30, 0, TimeSpan.Zero);
        Assert.True(session >= from && session < toExclusive);
    }

    [Fact]
    public void Early_morning_session_east_of_utc_is_not_leaked_into_the_previous_day()
    {
        const string zone = "Asia/Bangkok";
        // Logged 00:30 Bangkok on the 15th = 17:30 UTC on the 14th: belongs to the 15th, not the 14th.
        var session = new DateTimeOffset(2026, 1, 14, 17, 30, 0, TimeSpan.Zero);

        var the15th = (LocalDayResolver.StartOfLocalDayUtc(new DateOnly(2026, 1, 15), zone),
                       LocalDayResolver.StartOfLocalDayUtc(new DateOnly(2026, 1, 16), zone));
        var the14th = (LocalDayResolver.StartOfLocalDayUtc(new DateOnly(2026, 1, 14), zone),
                       LocalDayResolver.StartOfLocalDayUtc(new DateOnly(2026, 1, 15), zone));

        Assert.True(session >= the15th.Item1 && session < the15th.Item2);
        Assert.False(session >= the14th.Item1 && session < the14th.Item2);
    }
}
