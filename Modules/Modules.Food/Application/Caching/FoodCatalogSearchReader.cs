using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Mapping;
using Modules.FoodModule.Application.Queries;
using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Application.Caching;

/// <summary>Shared food-catalog search query used by the catalog cache service. Runs under the ambient
/// EF tenant filter (global + the active tenant's custom foods; admin sees everything), so the cache must
/// key its results by scope — see <see cref="FoodCatalogCache"/>.</summary>
public sealed class FoodCatalogSearchReader(IFoodRepository repository)
{
    public async Task<FoodListDto> LoadPageAsync(
        SearchFoodsQuery request,
        CancellationToken cancellationToken)
    {
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

        return new FoodListDto(items, page, pageSize, totalCount);
    }
}
