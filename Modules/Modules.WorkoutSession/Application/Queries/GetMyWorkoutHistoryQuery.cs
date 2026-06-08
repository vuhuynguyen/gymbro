using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries;

// Unified personal workout history: the caller's own sessions across every gym, paged. Self-scoped
// (no tenant context); classified ImperativeGuarded in TenantAuthorizationExemptions.
public sealed record GetMyWorkoutHistoryQuery(
    DateOnly? From,
    DateOnly? To,
    SessionStatus? Status,
    int Page,
    int PageSize) : IRequest<Result<SessionListDto>>;
