namespace BuildingBlocks.Shared.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdmin { get; }

    /// <summary>
    /// The caller's IANA time zone (e.g. <c>"America/Toronto"</c>) from the access token's <c>tz</c> claim, or
    /// null if unset. Handlers read this to bucket the caller's own days/weeks in their zone — so timezone is
    /// resolved server-side from the stored per-user setting, never a per-request client parameter.
    /// </summary>
    string? TimeZoneId { get; }
}
