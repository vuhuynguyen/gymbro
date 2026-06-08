using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// Self-scoped, cross-gym: the active session is now a per-user invariant (one in-progress workout per
// person, regardless of gym), so this no longer requires a tenant context. The handler scopes to
// currentUser.UserId; classified ImperativeGuarded in TenantAuthorizationExemptions.
public sealed record GetActiveSessionQuery : IRequest<Result<ActiveSessionDto?>>;
