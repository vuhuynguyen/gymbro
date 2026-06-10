using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class GetFoodByIdHandler(
    IFoodRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth)
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

        var food = await repository.Query()
            .Where(f => f.Id == request.Id)
            .Select(FoodMapping.FoodDtoProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return food == null
            ? Result<FoodDto>.Failure(NotFound("NotFound", "Food not found."))
            : Result<FoodDto>.Success(food);
    }
}
