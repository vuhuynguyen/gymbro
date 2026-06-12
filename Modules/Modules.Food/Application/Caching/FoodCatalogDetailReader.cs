using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Abstractions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Application.Mapping;

namespace Modules.FoodModule.Application.Caching;

/// <summary>Food detail query used by the catalog cache service. Runs under the ambient EF tenant filter
/// (so a tenant can never load another gym's custom food), hence the scope-keyed cache entries.</summary>
public sealed class FoodCatalogDetailReader(IFoodRepository repository)
{
    public async Task<FoodDto?> LoadAsync(Guid foodId, CancellationToken cancellationToken) =>
        await repository.Query()
            .Where(f => f.Id == foodId)
            .Select(FoodMapping.FoodDtoProjection)
            .FirstOrDefaultAsync(cancellationToken);
}
