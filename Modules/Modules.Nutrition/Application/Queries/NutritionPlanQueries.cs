using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

/// <summary>
/// Lists the tenant's nutrition plans (latest version per template). Gated on <c>NutritionPlanCreate</c>:
/// nutrition plans are coach-authored, and (unlike workout plans) trainees consume them through their daily
/// log, not by reading the raw template — so plan reads are an Owner-only authoring capability in MVP.
/// </summary>
public sealed record ListNutritionPlansQuery(string? Search = null, int Page = 1, int PageSize = 10)
    : IRequest<Result<NutritionPlanListDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanCreate;
}

public sealed record GetNutritionPlanByIdQuery(Guid Id)
    : IRequest<Result<NutritionPlanDetailDto>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.NutritionPlanCreate;
}
