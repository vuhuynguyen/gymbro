using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

public sealed record GetSessionByIdQuery(Guid SessionId) : IRequest<Result<SessionDetailDto>>;
