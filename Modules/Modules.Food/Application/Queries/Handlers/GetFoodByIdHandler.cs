using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Application.DTOs;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class GetFoodByIdHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    FoodCatalogCache catalogCache)
    : IRequestHandler<GetFoodByIdQuery, Result<FoodDto>>
{
    public async Task<Result<FoodDto>> Handle(GetFoodByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            if (!tenantContext.TenantId.HasValue)
                return Result<FoodDto>.Failure(
                    Validation("TenantContext.TenantId", "Tenant context is required."));

            var canView = await tenantAuth.HasPermissionAsync(
                tenantContext.TenantId.Value, Permission.PlanView, cancellationToken);
            if (!canView)
                return Result<FoodDto>.Failure(
                    Unauthorized("Food.Get.Unauthorized", "You do not have permission to view the food catalog."));
        }

        var envelope = await catalogCache.GetDetailAsync(request.Id, cancellationToken);

        return envelope is null || !envelope.Exists
            ? Result<FoodDto>.Failure(NotFound("NotFound", "Food not found."))
            : Result<FoodDto>.Success(envelope.Value!);
    }
}
