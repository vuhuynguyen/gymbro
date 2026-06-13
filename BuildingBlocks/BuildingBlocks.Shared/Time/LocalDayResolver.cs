namespace BuildingBlocks.Shared.Time;

/// <summary>
/// Resolves the local calendar day an instant falls on for a given IANA time-zone id — the single anchor for
/// day/week bucketing, so a trainee's session/day is attributed to THEIR local date, never UTC and never the
/// viewer's zone. Falls back to UTC when the zone is absent or unrecognised (e.g. a legacy abbreviation), so a
/// bad value degrades to the prior behaviour instead of throwing.
/// </summary>
public static class LocalDayResolver
{
    /// <summary>The calendar date <paramref name="instant"/> falls on in <paramref name="ianaZone"/> (UTC if unknown).</summary>
    public static DateOnly LocalDateOf(DateTimeOffset instant, string? ianaZone)
    {
        if (!string.IsNullOrWhiteSpace(ianaZone)
            && TimeZoneInfo.TryFindSystemTimeZoneById(ianaZone, out var tz))
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, tz).DateTime);

        return DateOnly.FromDateTime(instant.UtcDateTime);
    }
}
