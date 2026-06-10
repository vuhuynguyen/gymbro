using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

/// <summary>Lists nutrition-plan assignments in the gym (coach surface). Gated on <c>NutritionPlanAssign</c>.</summary>
public sealed record ListNutritionAssignmentsQuery(
    Guid? TraineeId,
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<NutritionAssignmentListDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanAssign;
}
