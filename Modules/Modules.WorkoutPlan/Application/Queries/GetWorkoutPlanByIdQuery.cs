using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

public sealed record GetWorkoutPlanByIdQuery(Guid Id) : IRequest<Result<WorkoutPlanDetailDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanView;
}
