using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Mapping;
using Modules.FoodModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.FoodModule.Application.Queries.Handlers;

public sealed class SearchFoodsHandler(
    IFoodRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth)
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

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Provider-agnostic case-insensitive contains (translates to LIKE lower(...) on Postgres);
            // keeps the module free of a Npgsql-specific EF.Functions.ILike dependency.
            var term = request.Search.Trim().ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(term)
                || (f.Brand != null && f.Brand.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Kind)
            && Enum.TryParse<FoodKind>(request.Kind, ignoreCase: true, out var kind))
        {
            query = query.Where(f => f.Kind == kind);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(FoodMapping.FoodDtoProjection)
            .ToListAsync(cancellationToken);

        return Result<FoodListDto>.Success(new FoodListDto(items, page, pageSize, totalCount));
    }
}
