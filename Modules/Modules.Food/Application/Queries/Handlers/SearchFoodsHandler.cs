using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.FoodModule.Application.Caching;
using Modules.FoodModule.Application.DTOs;
using static BuildingBlocks.Shared.Errors.Error;

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
        // Clamp pagination before it reaches the cache key / reader (DoS + cache-key cardinality). (Audit finding 3.)
        request = request with
        {
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };

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
