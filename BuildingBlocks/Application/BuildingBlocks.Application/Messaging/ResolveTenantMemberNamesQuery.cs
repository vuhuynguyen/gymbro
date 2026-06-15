using BuildingBlocks.Shared.Results;
using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>
/// Shared cross-module contract (handled by the User module): the Client members of one tenant with their
/// display names, for the coach roster. A shared contract — like <see cref="GetUserTimeZoneQuery"/> — so the
/// WorkoutSession module need not reference the User module. INTERNAL: carries no caller-facing authorization
/// of its own; it is only ever reached in-process from the coach roster handler, which has already gated on
/// <c>WorkoutLogViewAll</c> for the active tenant. Returns only <c>TenantRole.Client</c> rows; a member with
/// no <c>User</c> row degrades to "Unknown member" rather than vanishing.
/// </summary>
public sealed record ResolveTenantMemberNamesQuery(Guid TenantId)
    : IRequest<Result<IReadOnlyList<TenantMemberNameDto>>>;

/// <summary>One roster member: the trainee's id, resolved display name, and stored IANA time zone (null when
/// unset). The zone is folded in here — instead of a per-member <see cref="GetUserTimeZoneQuery"/> round-trip —
/// because this query already loads exactly these user rows; the roster buckets each member's weeks in it.</summary>
public sealed record TenantMemberNameDto(Guid UserId, string DisplayName, string? TimeZoneId);
