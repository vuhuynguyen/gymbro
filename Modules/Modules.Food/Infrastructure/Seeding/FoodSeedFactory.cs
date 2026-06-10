using Modules.FoodModule.Entities;

namespace Modules.FoodModule.Infrastructure.Seeding;

/// <summary>
/// Maps a validated <see cref="FoodSeedDto"/> onto the <see cref="Food"/> aggregate via its domain factory /
/// update method only (no reflection, no schema bypass). Supports creating a new global food and applying the
/// seed to an existing one in place (preserving its Id so plan/log references stay valid). Mirrors
/// <c>ExerciseSeedFactory</c>.
/// </summary>
public static class FoodSeedFactory
{
    /// <summary>Creates a new global catalog food from the seed entry.</summary>
    public static Food Create(FoodSeedDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Food.CreateGlobal(
            name: dto.Name!.Trim(),
            kind: ParseKind(dto.Kind),
            servingLabel: dto.ServingLabel!.Trim(),
            servingSizeGrams: dto.ServingSizeGrams,
            energyKcal: dto.EnergyKcal,
            proteinG: dto.ProteinG,
            carbsG: dto.CarbsG,
            fatG: dto.FatG,
            fiberG: dto.FiberG,
            brand: dto.Brand);
    }

    /// <summary>Applies the seed entry onto an existing tracked food in place (keeps its Id).</summary>
    public static void Apply(Food existing, FoodSeedDto dto)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(dto);
        existing.UpdateDetails(
            name: dto.Name!.Trim(),
            kind: ParseKind(dto.Kind),
            servingLabel: dto.ServingLabel!.Trim(),
            servingSizeGrams: dto.ServingSizeGrams,
            energyKcal: dto.EnergyKcal,
            proteinG: dto.ProteinG,
            carbsG: dto.CarbsG,
            fatG: dto.FatG,
            fiberG: dto.FiberG,
            brand: dto.Brand);
    }

    private static FoodKind ParseKind(string? value) =>
        Enum.TryParse<FoodKind>(value?.Trim(), ignoreCase: true, out var kind) ? kind : FoodKind.Food;
}
