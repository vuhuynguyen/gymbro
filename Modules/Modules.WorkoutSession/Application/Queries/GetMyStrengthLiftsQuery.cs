using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// SELF-SCOPED (no tenant context): the FULL list of strength lifts the caller performed in the window — the
/// uncapped sibling of the overview's top-3 strip. Computed across all of the caller's gyms via
/// <c>QueryOwnAcrossGyms(currentUser.UserId)</c>, windowed by <see cref="Weeks"/> exactly like the overview
/// (clamped to [4, 52], default 12). Each lift is enriched with its primary muscle group (resolved
/// cross-module) and an honesty-gated trend: a lift with fewer than 4 qualifying sessions reports e1RM +
/// session count only, never a fabricated direction. <see cref="MuscleGroup"/> (camelCase, tolerant-parsed and
/// ignored if unknown) optionally narrows the list to lifts whose PRIMARY group matches. Classified
/// ImperativeGuarded in TenantAuthorizationExemptions; a brand-new user returns 200 with an empty list.
/// </summary>
public sealed record GetMyStrengthLiftsQuery(int? Weeks, string? MuscleGroup)
    : IRequest<Result<StrengthLiftListDto>>;
