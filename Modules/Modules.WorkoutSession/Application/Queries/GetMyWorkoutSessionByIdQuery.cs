using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// Self-scoped detail for the unified history: resolves only when the session belongs to the caller.
public sealed record GetMyWorkoutSessionByIdQuery(Guid SessionId) : IRequest<Result<SessionDetailDto>>;
