using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

public sealed record ListPlanAssignmentsQuery(
    Guid? TraineeId,
    int Page = 1,
    int PageSize = 10) : IRequest<Result<PlanAssignmentListDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanView;
}
