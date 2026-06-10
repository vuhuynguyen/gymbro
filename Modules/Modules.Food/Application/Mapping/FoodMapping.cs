using System.Linq.Expressions;
using Modules.FoodModule.Application.DTOs;
using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Application.Mapping;

internal static class FoodMapping
{
    public static Expression<Func<Food, FoodDto>> FoodDtoProjection =>
        f => new FoodDto(
            f.Id,
            f.Name,
            f.Brand,
            f.Kind.ToString(),
            f.ServingLabel,
            f.ServingSizeGrams,
            f.EnergyKcal,
            f.ProteinG,
            f.CarbsG,
            f.FatG,
            f.FiberG,
            f.TenantId != null);

    public static Expression<Func<Food, FoodSummaryDto>> FoodSummaryProjection =>
        f => new FoodSummaryDto(
            f.Id,
            f.Name,
            f.Kind.ToString(),
            f.ServingLabel,
            f.EnergyKcal,
            f.ProteinG,
            f.CarbsG,
            f.FatG,
            f.FiberG);
}
