using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

public sealed record ListWorkoutPlansQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 10) : IRequest<Result<WorkoutPlanListDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanView;
}
