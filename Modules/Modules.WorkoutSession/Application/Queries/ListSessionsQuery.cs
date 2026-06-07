using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries;

public sealed record ListSessionsQuery(
    Guid? TraineeId,
    DateOnly? From,
    DateOnly? To,
    SessionStatus? Status,
    Guid? PlanAssignmentId,
    int Page,
    int PageSize) : IRequest<Result<SessionListDto>>;
