namespace Modules.FoodModule.Application.DTOs;

/// <summary>Catalog read model for a food/supplement. Macros are per the food's canonical serving.</summary>
public sealed record FoodDto(
    Guid Id,
    string Name,
    string? Brand,
    string Kind,
    string ServingLabel,
    decimal? ServingSizeGrams,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG,
    bool IsCustom);

public sealed record FoodListDto(
    IReadOnlyList<FoodDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
