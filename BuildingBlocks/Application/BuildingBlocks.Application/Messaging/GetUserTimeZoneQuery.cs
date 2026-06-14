using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>
/// Cross-module read of a user's stored IANA time zone (the authoritative domain <c>User.TimeZoneId</c>), used by
/// the Identity module to stamp the access token's <c>tz</c> claim at mint time. A shared contract so Identity
/// need not reference the User module. Returns null when unset.
/// </summary>
public sealed record GetUserTimeZoneQuery(Guid UserId) : IRequest<string?>;
