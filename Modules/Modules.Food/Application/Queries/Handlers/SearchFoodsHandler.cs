using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Application.DTOs;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class SearchFoodsHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    FoodCatalogCache catalogCache)
    : IRequestHandler<SearchFoodsQuery, Result<FoodListDto>>
{
    public async Task<Result<FoodListDto>> Handle(SearchFoodsQuery request, CancellationToken cancellationToken)
    {
        // Admin reads the whole catalog; otherwise the caller needs a validated tenant + PlanView.
        if (!currentUser.IsAdmin)
        {
            if (!tenantContext.TenantId.HasValue)
                return Result<FoodListDto>.Failure(
                    Validation("TenantContext.TenantId", "Tenant context is required."));

            var canView = await tenantAuth.HasPermissionAsync(
                tenantContext.TenantId.Value, Permission.PlanView, cancellationToken);
            if (!canView)
                return Result<FoodListDto>.Failure(
                    Unauthorized("Food.Search.Unauthorized", "You do not have permission to view the food catalog."));
        }

        var page = await catalogCache.GetSearchPageAsync(request, cancellationToken);
        return Result<FoodListDto>.Success(page);
    }
}
