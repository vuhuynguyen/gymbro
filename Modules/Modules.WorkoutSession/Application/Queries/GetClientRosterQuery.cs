using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// COACH, TENANT-SCOPED (own gym only): the needs-attention roster for the active tenant. Requires
/// <c>X-Tenant-Id</c>; gated by <c>WorkoutLogViewAll</c> in the handler. Every per-client signal is computed
/// over TENANT-FILTERED sessions (EF tenant filter ON) — cross-gym training is invisible by design
/// (FEASIBILITY R2). Classified ImperativeGuarded in TenantAuthorizationExemptions. Returns 200 with empty
/// Items when the gym has no members with sessions.
/// </summary>
public sealed record GetClientRosterQuery : IRequest<Result<RosterDto>>;
