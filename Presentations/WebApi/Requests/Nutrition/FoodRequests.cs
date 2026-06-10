namespace WebApi.Requests.Nutrition;

/// <summary>Editable fields for a food/supplement (create + update).</summary>
public sealed class FoodRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Food";
    public string ServingLabel { get; set; } = string.Empty;
    public decimal? ServingSizeGrams { get; set; }
    public decimal? EnergyKcal { get; set; }
    public decimal? ProteinG { get; set; }
    public decimal? CarbsG { get; set; }
    public decimal? FatG { get; set; }
    public decimal? FiberG { get; set; }
    public string? Brand { get; set; }
}
